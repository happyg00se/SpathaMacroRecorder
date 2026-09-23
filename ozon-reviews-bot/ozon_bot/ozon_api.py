"""Отзывы и вопросы через Ozon Seller API.

Методы отзывов и вопросов в Seller API работают при подписке Premium Plus / Premium Pro.
Без неё Ozon ответит 403 — тогда переключись на source: browser.
"""
from __future__ import annotations

import logging
import time

import requests

from .models import Item

log = logging.getLogger(__name__)
API = "https://api-seller.ozon.ru"


class OzonError(Exception):
    pass


class OzonApi:
    def __init__(self, cfg: dict, session: requests.Session | None = None):
        api = cfg["ozon_api"]
        self.http = session or requests.Session()
        self.http.headers.update(
            {"Client-Id": str(api["client_id"]), "Api-Key": str(api["api_key"])}
        )
        self.limit = int(cfg.get("max_items_per_check", 30))
        self.check_reviews = cfg.get("check_reviews", True)
        self.check_questions = cfg.get("check_questions", True)
        self._names: dict[str, str] = {}

    # ─── HTTP ────────────────────────────────────────────────────────────────
    def _post(self, path: str, body: dict) -> dict:
        for attempt in range(3):
            try:
                resp = self.http.post(API + path, json=body, timeout=60)
            except requests.RequestException as e:
                if attempt == 2:
                    raise OzonError(f"Ozon недоступен: {e}") from e
                time.sleep(2 * (attempt + 1))
                continue
            if resp.status_code == 429 and attempt < 2:
                time.sleep(3 * (attempt + 1))
                continue
            if resp.status_code == 403:
                raise OzonError(
                    f"Ozon запретил {path} (403). Скорее всего, нужна подписка Premium Plus "
                    f"или у ключа нет прав. Ответ: {resp.text[:200]}"
                )
            if resp.status_code >= 400:
                raise OzonError(f"Ozon {path}: {resp.status_code} {resp.text[:300]}")
            return resp.json() if resp.content else {}
        raise OzonError(f"Ozon {path}: слишком много запросов")

    # ─── чтение ──────────────────────────────────────────────────────────────
    def fetch_new(self) -> list[Item]:
        items: list[Item] = []
        errors = []
        if self.check_reviews:
            try:
                items += self._reviews()
            except OzonError as e:
                errors.append(str(e))
        if self.check_questions:
            try:
                items += self._questions()
            except OzonError as e:
                errors.append(str(e))
        if errors and not items:
            raise OzonError("; ".join(errors))
        for e in errors:
            log.warning(e)
        self._fill_names(items)
        return items

    def _reviews(self) -> list[Item]:
        out, last_id = [], ""
        while len(out) < self.limit:
            data = self._post(
                "/v1/review/list",
                {"last_id": last_id, "limit": 100, "sort_dir": "DESC", "status": "UNPROCESSED"},
            )
            for r in data.get("reviews") or []:
                out.append(
                    Item(
                        kind="review",
                        id=str(r["id"]),
                        text=r.get("text") or "",
                        sku=str(r.get("sku") or ""),
                        rating=r.get("rating"),
                        published_at=r.get("published_at") or "",
                        extra={"photos": r.get("photos_amount", 0), "videos": r.get("videos_amount", 0)},
                    )
                )
            last_id = data.get("last_id") or ""
            if not data.get("has_next") or not last_id:
                break
        return out[: self.limit]

    def _questions(self) -> list[Item]:
        out, last_id = [], ""
        while len(out) < self.limit:
            data = self._post(
                "/v1/question/list", {"filter": {"status": "UNPROCESSED"}, "last_id": last_id}
            )
            questions = data.get("questions") or []
            for q in questions:
                out.append(
                    Item(
                        kind="question",
                        id=str(q["id"]),
                        text=q.get("text") or "",
                        sku=str(q.get("sku") or ""),
                        author=q.get("author_name") or "",
                        published_at=q.get("published_at") or "",
                        extra={"url": q.get("question_link") or q.get("product_url") or ""},
                    )
                )
            new_last = data.get("last_id") or ""
            if not questions or not new_last or new_last == last_id:
                break
            last_id = new_last
        return out[: self.limit]

    def _fill_names(self, items: list[Item]) -> None:
        """Название товара помогает нейросети. Не получилось — не страшно."""
        skus = sorted({i.sku for i in items if i.sku and i.sku not in self._names})
        for chunk in (skus[n : n + 100] for n in range(0, len(skus), 100)):
            try:
                data = self._post("/v3/product/info/list", {"sku": [int(s) for s in chunk]})
            except (OzonError, ValueError) as e:
                log.info("Названия товаров не получены: %s", e)
                break
            for p in data.get("items") or []:
                name = p.get("name") or ""
                for sku in [p.get("sku")] + [s.get("sku") for s in p.get("sources") or []]:
                    if sku:
                        self._names[str(sku)] = name
        for i in items:
            i.product_name = i.product_name or self._names.get(i.sku, "")

    # ─── публикация ──────────────────────────────────────────────────────────
    def publish(self, item: Item, text: str) -> None:
        if item.kind == "review":
            self._post(
                "/v1/review/comment/create",
                {"review_id": item.id, "text": text, "mark_review_as_processed": True},
            )
        else:
            self._post(
                "/v1/question/answer/create",
                {"question_id": item.id, "sku": int(item.sku), "text": text},
            )

    def close(self) -> None:
        self.http.close()
