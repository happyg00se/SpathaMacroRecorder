"""Связка: Ozon → локальная нейросеть → Ozon, с управлением и подтверждением в Telegram.

Вся работа с Ozon и нейросетью идёт в одном рабочем потоке по очереди (браузер Playwright
нельзя дёргать из разных потоков). Telegram-поток только принимает команды и кладёт задачи в очередь.
"""
from __future__ import annotations

import logging
import queue
import threading
import time
from pathlib import Path

from . import storage as st
from .llm import LLMError, LocalLLM
from .models import Draft, Item
from .storage import Storage
from .telegram import Telegram, esc

log = logging.getLogger(__name__)

SENTIMENT_ICON = {"positive": "🟢", "neutral": "🟡", "negative": "🔴"}
MODE_RU = {
    "all": "всё публикую сам",
    "safe": "позитив публикую сам, негатив — на твои кнопки",
    "manual": "ничего не публикую без твоей кнопки",
}

HELP = """<b>Команды</b>
/check — проверить отзывы и вопросы прямо сейчас
/status — что сделано и что ждёт тебя
/pending — прислать заново всё, что ждёт решения
/mode_all · /mode_safe · /mode_manual — когда публиковать без спроса
/pause · /resume — остановить или продолжить автопроверку

Под каждым ответом кнопки:
✅ опубликовать · 🔄 переписать · ✏️ свой текст или указание · ⏭ пропустить"""


class App:
    def __init__(self, cfg: dict, source, llm: LocalLLM, tg: Telegram, db: Storage):
        self.cfg = cfg
        self.source = source
        self.llm = llm
        self.tg = tg
        self.db = db
        self.chats: list[int] = [int(c) for c in (cfg["telegram"].get("allowed_chat_ids") or [])]
        self.poll_seconds = max(1, int(cfg.get("poll_minutes", 15))) * 60
        self.jobs: queue.Queue = queue.Queue()
        self._stop = threading.Event()
        self._busy = threading.Lock()

    # ─── настройки, которые меняются из Telegram ─────────────────────────────
    @property
    def mode(self) -> str:
        return self.db.get_kv("mode") or self.cfg.get("auto_publish", "safe")

    @property
    def paused(self) -> bool:
        return self.db.get_kv("paused") == "1"

    # ─── главный цикл ────────────────────────────────────────────────────────
    def run(self) -> None:
        worker = threading.Thread(target=self._worker, name="worker", daemon=True)
        worker.start()
        self.notify(f"🤖 Бот запущен. Режим: <b>{MODE_RU[self.mode]}</b>.\n\n{HELP}")
        try:
            self._telegram_loop()
        except KeyboardInterrupt:
            pass
        finally:
            self._stop.set()
            self.jobs.put(None)

    def _worker(self) -> None:
        next_check = 0.0
        while not self._stop.is_set():
            timeout = max(0.0, next_check - time.time())
            try:
                job = self.jobs.get(timeout=timeout)
            except queue.Empty:
                job = "auto-check"
            if job is None:
                return
            try:
                if job == "auto-check":
                    next_check = time.time() + self.poll_seconds
                    if not self.paused:
                        self.check()
                else:
                    job()
            except Exception as e:  # noqa: BLE001 — рабочий поток не должен умирать
                log.exception("Ошибка в задаче")
                self.notify(f"⚠️ Ошибка: {esc(str(e))}")

    def _telegram_loop(self) -> None:
        offset = int(self.db.get_kv("tg_offset", "0"))
        while not self._stop.is_set():
            updates = self.tg.get_updates(offset)
            if not updates:
                time.sleep(1)
            for upd in updates:
                offset = upd["update_id"] + 1
                self.db.set_kv("tg_offset", str(offset))
                try:
                    self.handle_update(upd)
                except Exception:  # noqa: BLE001
                    log.exception("Ошибка обработки Telegram-сообщения")

    # ─── проверка новых отзывов ──────────────────────────────────────────────
    def check(self, manual: bool = False) -> dict:
        with self._busy:
            items = self.source.fetch_new()
            fresh = [i for i in items if self._is_new(i)]
            stats = {"new": len(fresh), "published": 0, "pending": 0, "skipped": 0, "errors": 0}
            for item in fresh:
                result = self.process(item)
                stats[result] = stats.get(result, 0) + 1
            if fresh or manual:
                self.notify(
                    f"📬 Проверка: новых {stats['new']}, опубликовано {stats['published']}, "
                    f"ждут тебя {stats['pending']}, пропущено {stats['skipped']}, ошибок {stats['errors']}"
                )
            return stats

    def _is_new(self, item: Item) -> bool:
        row = self.db.get(item.key)
        # отзыв, на котором нейросеть в прошлый раз упала, пробуем снова
        return row is None or (row[1] == st.ERROR and row[2] is None)

    def process(self, item: Item, hint: str = "") -> str:
        self.db.add(item)
        if item.kind == "review" and not item.text.strip() and self.cfg.get("empty_review", "thank") == "skip":
            self.db.update(item.key, status=st.SKIPPED)
            return "skipped"
        try:
            draft = self.llm.draft(item, hint)
        except LLMError as e:
            self.db.update(item.key, status=st.ERROR, error=str(e))
            self.notify(f"⚠️ {item.kind_ru} {esc(item.id)}: {esc(str(e))}")
            return "errors"
        self.db.update(item.key, draft=draft)

        if self.should_autopublish(item, draft):
            if self.publish(item, draft.reply):
                self.notify(self.card(item, draft, header="✅ Опубликовано автоматически"))
                return "published"
            return "errors"
        self.ask(item, draft)
        return "pending"

    def should_autopublish(self, item: Item, draft: Draft) -> bool:
        if draft.needs_human:
            return False
        if self.mode == "all":
            return True
        if self.mode == "manual":
            return False
        # safe: только то, что по смыслу не негатив. Отзыв без текста — тут звёзды единственный сигнал.
        if item.kind == "review" and not item.text.strip():
            return (item.rating or 0) >= 4
        return draft.sentiment in ("positive", "neutral")

    def publish(self, item: Item, text: str) -> bool:
        try:
            self.source.publish(item, text)
        except Exception as e:  # noqa: BLE001
            log.exception("Не удалось опубликовать %s", item.key)
            self.db.update(item.key, status=st.ERROR, error=str(e))
            self.notify(
                f"⚠️ Не удалось опубликовать ответ на {item.kind_ru.lower()} {esc(item.id)}: {esc(str(e))}",
                buttons=[[("🔁 Ещё раз", f"ok|{item.key}"), ("⏭ Пропустить", f"skip|{item.key}")]],
            )
            return False
        self.db.update(item.key, status=st.PUBLISHED, error=None)
        return True

    # ─── Telegram: карточки ──────────────────────────────────────────────────
    def card(self, item: Item, draft: Draft | None, header: str = "") -> str:
        lines = []
        if header:
            lines.append(f"<b>{header}</b>")
        title = f"{item.kind_ru}"
        if item.rating is not None:
            title += " " + "★" * int(item.rating) + "☆" * (5 - int(item.rating))
        if draft:
            title = f"{SENTIMENT_ICON.get(draft.sentiment, '')} {title}"
            if draft.topic:
                title += f" · {esc(draft.topic)}"
        lines.append(title)
        if item.product_name:
            lines.append(f"📦 {esc(item.product_name)}")
        lines.append(f"\n💬 <i>{esc(item.text.strip()) or '(без текста)'}</i>")
        if draft:
            lines.append(f"\n🤖 {esc(draft.reply)}")
            if draft.needs_human and draft.reason:
                lines.append(f"\n❗️ {esc(draft.reason)}")
        return "\n".join(lines)

    def ask(self, item: Item, draft: Draft) -> None:
        buttons = [
            [("✅ Опубликовать", f"ok|{item.key}"), ("🔄 Переписать", f"re|{item.key}")],
            [("✏️ Свой текст", f"ed|{item.key}"), ("⏭ Пропустить", f"skip|{item.key}")],
        ]
        text = self.card(item, draft, header="Нужно твоё решение")
        chat_id, message_id = None, None
        for chat in self.chats:
            mid = self.tg.send(chat, text, buttons)
            if mid and chat_id is None:
                chat_id, message_id = chat, mid
        self.db.update(item.key, status=st.PENDING, tg_chat_id=chat_id, tg_message_id=message_id)

    def notify(self, text: str, buttons=None) -> None:
        for chat in self.chats:
            self.tg.send(chat, text, buttons)

    # ─── Telegram: входящие ──────────────────────────────────────────────────
    def handle_update(self, upd: dict) -> None:
        if "callback_query" in upd:
            cq = upd["callback_query"]
            chat_id = cq["message"]["chat"]["id"]
            if chat_id not in self.chats:
                self.tg.answer_callback(cq["id"], "Нет доступа")
                return
            self.on_button(cq["data"], chat_id, cq["message"]["message_id"], cq["id"])
        elif "message" in upd:
            msg = upd["message"]
            chat_id = msg["chat"]["id"]
            text = (msg.get("text") or "").strip()
            if chat_id not in self.chats:
                if text.startswith("/start"):
                    self.tg.send(chat_id, f"Твой chat id: <code>{chat_id}</code>\nДобавь его в telegram.allowed_chat_ids в config.yaml и перезапусти бота.")
                return
            self.on_message(chat_id, text)

    def on_button(self, data: str, chat_id: int, message_id: int, callback_id: str) -> None:
        action, _, key = data.partition("|")
        row = self.db.get(key)
        if row is None:
            self.tg.answer_callback(callback_id, "Не нашёл этот отзыв в базе")
            return
        item, status, draft, _ = row
        if status in (st.PUBLISHED, st.SKIPPED):
            self.tg.answer_callback(callback_id, "Уже обработано")
            return

        if action == "ok":
            if not draft:
                self.tg.answer_callback(callback_id, "Нет текста ответа — нажми «Переписать»")
                return
            self.tg.answer_callback(callback_id, "Публикую…")
            self.jobs.put(lambda: self._publish_from_button(item, draft, chat_id, message_id))
        elif action == "re":
            self.tg.answer_callback(callback_id, "Переписываю…")
            self.jobs.put(lambda: self._redraft(item, "", chat_id, message_id))
        elif action == "ed":
            self.db.set_kv(f"await_edit:{chat_id}", f"{key}|{message_id}")
            self.tg.answer_callback(callback_id)
            self.tg.send(
                chat_id,
                "Пришли текст ответа — опубликую как есть.\n"
                "Или начни с <b>?</b> — это будет указание нейросети, например:\n"
                "<code>? предложи прислать фото брака в чат</code>\n/cancel — отмена",
            )
        elif action == "skip":
            self.db.update(key, status=st.SKIPPED)
            self.tg.answer_callback(callback_id, "Пропущено")
            self.tg.edit(chat_id, message_id, self.card(item, draft, header="⏭ Пропущено"))

    def _publish_from_button(self, item: Item, draft: Draft, chat_id: int, message_id: int) -> None:
        if self.publish(item, draft.reply):
            self.tg.edit(chat_id, message_id, self.card(item, draft, header="✅ Опубликовано"))

    def _redraft(self, item: Item, hint: str, chat_id: int, message_id: int) -> None:
        try:
            draft = self.llm.draft(item, hint)
        except LLMError as e:
            self.tg.send(chat_id, f"⚠️ {esc(str(e))}")
            return
        self.db.update(item.key, draft=draft, status=st.PENDING)
        buttons = [
            [("✅ Опубликовать", f"ok|{item.key}"), ("🔄 Переписать", f"re|{item.key}")],
            [("✏️ Свой текст", f"ed|{item.key}"), ("⏭ Пропустить", f"skip|{item.key}")],
        ]
        self.tg.edit(chat_id, message_id, self.card(item, draft, header="Новый вариант"), buttons)

    def on_message(self, chat_id: int, text: str) -> None:
        waiting = self.db.get_kv(f"await_edit:{chat_id}")
        if text.startswith("/"):
            if waiting:
                self.db.set_kv(f"await_edit:{chat_id}", "")
            self.command(chat_id, text.split()[0].split("@")[0].lower())
            return
        if not waiting:
            self.tg.send(chat_id, HELP)
            return

        self.db.set_kv(f"await_edit:{chat_id}", "")
        key, _, message_id = waiting.rpartition("|")
        row = self.db.get(key)
        if row is None:
            return
        item = row[0]
        message_id = int(message_id)
        if text.startswith("?"):
            hint = text[1:].strip()
            self.tg.send(chat_id, "Переписываю с учётом указания…")
            self.jobs.put(lambda: self._redraft(item, hint, chat_id, message_id))
        else:
            draft = Draft(reply=text, sentiment=(row[2].sentiment if row[2] else "neutral"), topic=(row[2].topic if row[2] else ""))
            self.db.update(key, draft=draft)
            self.tg.send(chat_id, "Публикую твой текст…")
            self.jobs.put(lambda: self._publish_from_button(item, draft, chat_id, message_id))

    def command(self, chat_id: int, cmd: str) -> None:
        if cmd in ("/start", "/help"):
            self.tg.send(chat_id, f"Режим: <b>{MODE_RU[self.mode]}</b>{' (на паузе)' if self.paused else ''}\n\n{HELP}")
        elif cmd == "/check":
            self.tg.send(chat_id, "Проверяю…")
            self.jobs.put(lambda: self.check(manual=True))
        elif cmd == "/status":
            c = self.db.counts()
            self.tg.send(
                chat_id,
                f"Режим: <b>{MODE_RU[self.mode]}</b>{' — <b>пауза</b>' if self.paused else ''}\n"
                f"Опубликовано: {c.get(st.PUBLISHED, 0)}\nЖдут тебя: {c.get(st.PENDING, 0)}\n"
                f"Пропущено: {c.get(st.SKIPPED, 0)}\nОшибок: {c.get(st.ERROR, 0)}\n"
                f"Проверка каждые {self.poll_seconds // 60} мин.",
            )
        elif cmd == "/pending":
            keys = self.db.keys_with_status(st.PENDING) + self.db.keys_with_status(st.ERROR)
            if not keys:
                self.tg.send(chat_id, "Ничего не ждёт — всё разобрано 👌")
            for key in keys:
                item, _, draft, _ = self.db.get(key)
                if draft:
                    self.ask(item, draft)
                else:
                    self.jobs.put(lambda item=item: self.process(item))
        elif cmd.startswith("/mode_") and cmd[6:] in MODE_RU:
            self.db.set_kv("mode", cmd[6:])
            self.tg.send(chat_id, f"Режим: <b>{MODE_RU[cmd[6:]]}</b>")
        elif cmd == "/pause":
            self.db.set_kv("paused", "1")
            self.tg.send(chat_id, "⏸ Автопроверка на паузе. /check работает. /resume — продолжить.")
        elif cmd == "/resume":
            self.db.set_kv("paused", "0")
            self.tg.send(chat_id, "▶️ Автопроверка снова работает.")
            self.jobs.put("auto-check")
        elif cmd == "/cancel":
            self.tg.send(chat_id, "Ок, отменил.")
        else:
            self.tg.send(chat_id, HELP)


def build(cfg: dict) -> App:
    if cfg.get("source", "api") == "browser":
        from .ozon_browser import OzonBrowser

        source = OzonBrowser(cfg)
    else:
        from .ozon_api import OzonApi

        source = OzonApi(cfg)
    db = Storage(Path(cfg["data_dir"]) / "bot.sqlite3")
    return App(cfg, source, LocalLLM(cfg), Telegram(cfg["telegram"]["token"]), db)
