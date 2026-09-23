"""Отзывы и вопросы через кабинет seller.ozon.ru в Chrome — для тех, у кого нет Premium Plus.

Как читает: открывает страницу отзывов и перехватывает данные, которые кабинет сам грузит
с сервера Ozon (JSON). Если перехват ничего не дал — читает карточки со страницы по селекторам
из config.yaml.

Как отвечает: находит карточку отзыва на странице, жмёт «Ответить», вставляет текст, жмёт «Отправить».

Ozon меняет вёрстку кабинета без предупреждения. Если что-то перестало работать —
`python -m ozon_bot diagnose` сохранит страницу и перехваченные данные в data/diagnose/,
по ним правятся селекторы в config.yaml.
"""
from __future__ import annotations

import hashlib
import json
import logging
import re
import time
from contextlib import contextmanager
from pathlib import Path

from .models import Item

log = logging.getLogger(__name__)

_ID_KEYS = ("uuid", "id", "review_id", "reviewId", "question_id", "questionId")
_TEXT_KEYS = ("text", "comment", "body", "content", "question")
_RATING_KEYS = ("rating", "score", "grade")
_SKU_KEYS = ("sku", "product_sku", "productSku", "item_id", "itemId")
_NAME_KEYS = ("product_name", "productName", "product_title", "title", "name")
_AUTHOR_KEYS = ("author_name", "authorName", "author", "user_name", "userName")
_ANSWERED_KEYS = ("is_answered", "isAnswered", "has_answer", "hasAnswer", "answered")


class BrowserError(Exception):
    pass


class LoginRequired(BrowserError):
    """Ozon разлогинил — нужно войти заново (можно прямо с телефона через пульт)."""


class OzonBrowser:
    def __init__(self, cfg: dict):
        b = cfg.get("browser") or {}
        self.profile_dir = b.get("profile_dir", "data/chrome-profile")
        self.channel = b.get("channel", "chrome") or None
        self.executable_path = b.get("executable_path") or None
        self.headless = bool(b.get("headless", False))
        self.hide_window = bool(b.get("hide_window", True))
        self.restart_hours = float(b.get("restart_hours", 6))
        self.urls = {"review": b.get("reviews_url"), "question": b.get("questions_url")}
        self.sel = b.get("selectors") or {}
        self.limit = int(cfg.get("max_items_per_check", 30))
        self.check = {"review": cfg.get("check_reviews", True), "question": cfg.get("check_questions", True)}
        self.data_dir = Path(cfg.get("data_dir", "data"))
        self._pw = None
        self._ctx = None
        self._started = 0.0

    # ─── браузер ─────────────────────────────────────────────────────────────
    # Бот держит один Chrome открытым круглые сутки: так вход в кабинет не слетает, а проверка
    # идёт за секунды. Раз в restart_hours браузер перезапускается, чтобы не копил память.
    # Все вызовы — только из одного потока (рабочего потока бота).
    def _launch(self, headless: bool):
        try:
            from playwright.sync_api import sync_playwright
        except ImportError as e:
            raise BrowserError("Не установлен playwright: pip install playwright") from e
        pw = sync_playwright().start()
        args = ["--disable-blink-features=AutomationControlled"]
        if self.hide_window and not headless:
            args.append("--window-position=-32000,-32000")  # окно есть, но за краем экрана и не мешает
        try:
            ctx = pw.chromium.launch_persistent_context(
                self.profile_dir,
                channel=None if self.executable_path else self.channel,
                executable_path=self.executable_path,
                headless=headless,
                viewport={"width": 1400, "height": 900},
                locale="ru-RU",
                args=args,
            )
        except Exception as e:  # noqa: BLE001 — playwright кидает разные ошибки
            pw.stop()
            raise BrowserError(
                f"Chrome не запустился: {e}. Закрой другие окна бота и проверь, что установлен Google Chrome "
                f"(или поставь browser.channel пустым)."
            ) from e
        return pw, ctx

    @contextmanager
    def _page(self, headless: bool | None = None):
        if headless is not None:
            # разовый запуск для login/diagnose из консоли
            pw, ctx = self._launch(headless)
            try:
                yield ctx.pages[0] if ctx.pages else ctx.new_page()
            finally:
                ctx.close()
                pw.stop()
            return

        if self._ctx is not None and time.time() - self._started > self.restart_hours * 3600:
            self.close()
        page = None
        if self._ctx is not None:
            try:
                page = self._ctx.pages[0] if self._ctx.pages else self._ctx.new_page()
            except Exception:  # noqa: BLE001 — окно закрыли руками или Chrome упал
                self.close()
        if page is None:
            self._pw, self._ctx = self._launch(self.headless)
            self._started = time.time()
            page = self._ctx.pages[0] if self._ctx.pages else self._ctx.new_page()
        try:
            yield page
        except BrowserError:
            raise
        except Exception:
            self.close()  # что-то сломалось в самом браузере — в следующий раз поднимем заново
            raise

    def close(self) -> None:
        for obj, method in ((self._ctx, "close"), (self._pw, "stop")):
            if obj is not None:
                try:
                    getattr(obj, method)()
                except Exception:  # noqa: BLE001
                    pass
        self._ctx = self._pw = None

    # ─── пульт: управление кабинетом с телефона ──────────────────────────────
    def screenshot(self) -> bytes:
        with self._page() as page:
            return page.screenshot()

    def current_url(self) -> str:
        with self._page() as page:
            return page.url

    def open(self, target: str) -> bytes:
        url = {"reviews": self.urls["review"], "questions": self.urls["question"]}.get(target, target)
        if not re.match(r"https?://", url or ""):
            url = "https://" + url
        with self._page() as page:
            page.goto(url, wait_until="domcontentloaded")
            _settle(page)
            return page.screenshot()

    def click_text(self, text: str) -> bytes:
        with self._page() as page:
            candidates = [
                page.get_by_role("button", name=text),
                page.get_by_placeholder(text),
                page.get_by_label(text),
                page.get_by_text(text),
            ]
            for loc in candidates:
                try:
                    visible = loc.locator("visible=true")
                    if visible.count():
                        visible.first.click(timeout=10000)
                        break
                except Exception:  # noqa: BLE001
                    continue
            else:
                raise BrowserError(f"Не нашёл на странице «{text}». Пришли /screen и посмотри, как оно написано.")
            _settle(page)
            return page.screenshot()

    def type_text(self, text: str) -> bytes:
        with self._page() as page:
            page.keyboard.type(text, delay=60)
            page.wait_for_timeout(800)
            return page.screenshot()

    def press(self, key: str) -> bytes:
        with self._page() as page:
            page.keyboard.press(key)
            _settle(page)
            return page.screenshot()

    # ─── вход ────────────────────────────────────────────────────────────────
    def login(self) -> None:
        with self._page(headless=False) as page:
            page.goto(self.urls["review"] or "https://seller.ozon.ru/")
            input("Войди в кабинет Ozon в открывшемся окне, затем вернись сюда и нажми Enter… ")

    # ─── чтение ──────────────────────────────────────────────────────────────
    def fetch_new(self) -> list[Item]:
        items: list[Item] = []
        with self._page() as page:
            for kind in ("review", "question"):
                if self.check[kind] and self.urls[kind]:
                    items += self._fetch_kind(page, kind)
        return items

    def _fetch_kind(self, page, kind: str) -> list[Item]:
        captured: list = []

        def on_response(resp):
            if "json" not in (resp.headers.get("content-type") or ""):
                return
            try:
                captured.append(resp.json())
            except Exception:  # noqa: BLE001
                pass

        page.on("response", on_response)
        try:
            page.goto(self.urls[kind], wait_until="domcontentloaded")
            self._ensure_logged_in(page)
            page.wait_for_load_state("networkidle", timeout=30000)
        except BrowserError:
            raise
        except Exception as e:  # noqa: BLE001
            log.warning("Страница %s грузилась с ошибкой: %s", self.urls[kind], e)
        finally:
            page.remove_listener("response", on_response)

        items = []
        for payload in captured:
            items += extract_items(payload, kind)
        items = [i for i in _dedupe(items) if not i.extra.get("answered")]
        if not items:
            items = self._from_dom(page, kind)
        return items[: self.limit]

    def _ensure_logged_in(self, page) -> None:
        if re.search(r"/signin|/login|/auth(?:[/?#]|$)|id\.ozon\.ru", page.url):
            raise LoginRequired("Кабинет Ozon просит войти заново.")

    def _from_dom(self, page, kind: str) -> list[Item]:
        items = []
        for card in page.locator(self.sel.get("card", "[data-review-uuid]")).all():
            item_id = None
            for attr in self.sel.get("id_attrs") or []:
                item_id = card.get_attribute(attr)
                if item_id:
                    break
            text = _first_text(card, self.sel.get("text"))
            if not item_id:
                if not text:
                    continue
                item_id = "dom-" + hashlib.sha1(text.encode("utf-8")).hexdigest()[:16]  # запасной id, если кабинет его не показывает
            rating = None
            if kind == "review":
                m = re.search(r"[1-5]", _first_text(card, self.sel.get("rating")) or "")
                rating = int(m.group(0)) if m else None
            items.append(
                Item(
                    kind=kind,
                    id=item_id,
                    text=text or "",
                    product_name=_first_text(card, self.sel.get("product")) or "",
                    rating=rating,
                    extra={"source": "dom"},
                )
            )
        return items

    # ─── ответ ───────────────────────────────────────────────────────────────
    def publish(self, item: Item, text: str) -> None:
        with self._page() as page:
            page.goto(self.urls[item.kind], wait_until="domcontentloaded")
            self._ensure_logged_in(page)
            try:
                page.wait_for_load_state("networkidle", timeout=30000)
            except Exception:  # noqa: BLE001
                pass
            card = self._find_card(page, item)
            if card is None:
                raise BrowserError(
                    f"Не нашёл {item.kind_ru.lower()} {item.id} на странице кабинета. Ответь вручную или проверь селекторы."
                )
            card.scroll_into_view_if_needed()
            card.click()
            button = card.locator(self.sel.get("reply_button", "button:has-text('Ответить')"))
            if button.count() == 0:
                button = page.locator(self.sel.get("reply_button", "button:has-text('Ответить')"))
            if button.count():
                button.first.click()
            box = card.locator(self.sel.get("reply_input", "textarea"))
            if box.count() == 0:
                box = page.locator(self.sel.get("reply_input", "textarea"))
            if box.count() == 0:
                raise BrowserError("Не нашёл поле для ответа. Проверь browser.selectors.reply_input.")
            box.first.fill(text)
            send = page.locator(self.sel.get("send_button", "button:has-text('Отправить')"))
            if send.count() == 0:
                raise BrowserError("Не нашёл кнопку отправки. Проверь browser.selectors.send_button.")
            send.last.click()
            page.wait_for_timeout(2500)

    def _find_card(self, page, item: Item):
        for attr in self.sel.get("id_attrs") or []:
            loc = page.locator(f"[{attr}='{item.id}']")
            if loc.count():
                return loc.first
        snippet = item.text.strip()[:60]
        if snippet:
            loc = page.locator(self.sel.get("card", "div")).filter(has_text=snippet)
            if loc.count():
                return loc.first
            loc = page.get_by_text(snippet, exact=False)
            if loc.count():
                return loc.first.locator("xpath=ancestor::*[.//textarea or .//button][1]")
        return None

    # ─── диагностика ─────────────────────────────────────────────────────────
    def diagnose(self) -> Path:
        out = self.data_dir / "diagnose"
        out.mkdir(parents=True, exist_ok=True)
        with self._page(headless=False) as page:
            for kind, url in self.urls.items():
                if not url:
                    continue
                captured = []

                def on_response(resp, captured=captured):
                    if "json" in (resp.headers.get("content-type") or ""):
                        try:
                            captured.append({"url": resp.url, "data": resp.json()})
                        except Exception:  # noqa: BLE001
                            pass

                page.on("response", on_response)
                page.goto(url, wait_until="domcontentloaded")
                try:
                    page.wait_for_load_state("networkidle", timeout=30000)
                except Exception:  # noqa: BLE001
                    pass
                page.remove_listener("response", on_response)
                page.screenshot(path=str(out / f"{kind}.png"), full_page=True)
                (out / f"{kind}.html").write_text(page.content(), encoding="utf-8")
                (out / f"{kind}-responses.json").write_text(
                    json.dumps(captured, ensure_ascii=False, indent=1)[:5_000_000], encoding="utf-8"
                )
                found = []
                for c in captured:
                    found += extract_items(c["data"], kind)
                dom = self._from_dom(page, kind)
                print(f"{kind}: из данных кабинета — {len(found)}, со страницы — {len(dom)}")
                for i in (found or dom)[:3]:
                    print(f"   {i.id}: {i.text[:80]!r}")
        return out


# ─── разбор данных кабинета ──────────────────────────────────────────────────
def extract_items(payload, kind: str) -> list[Item]:
    """Ищет в JSON кабинета списки объектов, похожих на отзывы/вопросы."""
    found: list[Item] = []
    for obj in _walk_dicts(payload):
        item_id = _pick(obj, _ID_KEYS)
        text = _pick(obj, _TEXT_KEYS)
        if item_id is None or not isinstance(text, str):
            continue
        rating = _pick(obj, _RATING_KEYS)
        if kind == "review" and not isinstance(rating, (int, float)):
            continue  # у отзыва всегда есть оценка — так отсекаем посторонние объекты
        product = obj.get("product") if isinstance(obj.get("product"), dict) else {}
        author = _pick(obj, _AUTHOR_KEYS)
        if isinstance(author, dict):
            author = author.get("name") or ""
        answered = _pick(obj, _ANSWERED_KEYS)
        if answered is None:
            answers = obj.get("comments_amount", obj.get("answers_count"))
            answered = bool(answers) if isinstance(answers, int) else False
        found.append(
            Item(
                kind=kind,
                id=str(item_id),
                text=text,
                sku=str(_pick(obj, _SKU_KEYS) or _pick(product, _SKU_KEYS) or ""),
                product_name=str(_pick(product, _NAME_KEYS) or obj.get("product_name") or obj.get("productName") or ""),
                rating=int(rating) if isinstance(rating, (int, float)) else None,
                author=str(author or ""),
                published_at=str(obj.get("published_at") or obj.get("created_at") or obj.get("createdAt") or ""),
                extra={"answered": bool(answered), "source": "json"},
            )
        )
    return found


def _walk_dicts(node):
    if isinstance(node, dict):
        yield node
        for v in node.values():
            yield from _walk_dicts(v)
    elif isinstance(node, list):
        for v in node:
            yield from _walk_dicts(v)


def _pick(obj: dict, keys):
    for k in keys:
        if k in obj and obj[k] not in (None, ""):
            return obj[k]
    return None


def _dedupe(items: list[Item]) -> list[Item]:
    seen, out = set(), []
    for i in items:
        if i.key not in seen:
            seen.add(i.key)
            out.append(i)
    return out


def _first_text(card, selector: str | None) -> str:
    if not selector:
        return ""
    loc = card.locator(selector)
    try:
        return loc.first.inner_text(timeout=1000).strip() if loc.count() else ""
    except Exception:  # noqa: BLE001
        return ""


def _settle(page) -> None:
    """Даём странице догрузиться, но не ждём вечно: у кабинета бывают бесконечные фоновые запросы."""
    try:
        page.wait_for_load_state("networkidle", timeout=10000)
    except Exception:  # noqa: BLE001
        pass
    page.wait_for_timeout(700)
