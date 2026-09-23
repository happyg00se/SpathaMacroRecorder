"""Минимальный клиент Telegram Bot API на long polling — без лишних библиотек."""
from __future__ import annotations

import html
import logging

import requests

log = logging.getLogger(__name__)


class Telegram:
    def __init__(self, token: str, session: requests.Session | None = None):
        self.token = token
        self.base = f"https://api.telegram.org/bot{token}/"
        self.http = session or requests.Session()

    def call(self, method: str, http_timeout: float = 30, **params) -> dict | list | bool | None:
        try:
            resp = self.http.post(self.base + method, json=params, timeout=http_timeout)
            data = resp.json()
        except (requests.RequestException, ValueError) as e:
            # в тексте ошибки requests бывает URL с токеном — прячем его
            log.warning("Telegram %s: %s", method, str(e).replace(self.token, "***"))
            return None
        if not data.get("ok"):
            # «message is not modified» — не ошибка, просто нажали кнопку дважды
            if "not modified" not in str(data.get("description")):
                log.warning("Telegram %s: %s", method, data.get("description"))
            return None
        return data.get("result")

    def get_updates(self, offset: int, timeout: int = 50) -> list[dict]:
        return self.call(
            "getUpdates",
            http_timeout=timeout + 10,
            offset=offset,
            timeout=timeout,
            allowed_updates=["message", "callback_query"],
        ) or []

    def send(self, chat_id: int, text: str, buttons: list[list[tuple[str, str]]] | None = None) -> int | None:
        params = {"chat_id": chat_id, "text": text[:4096], "parse_mode": "HTML", "disable_web_page_preview": True}
        if buttons:
            params["reply_markup"] = keyboard(buttons)
        result = self.call("sendMessage", **params)
        return result.get("message_id") if isinstance(result, dict) else None

    def edit(self, chat_id: int, message_id: int, text: str, buttons: list[list[tuple[str, str]]] | None = None) -> None:
        params = {
            "chat_id": chat_id,
            "message_id": message_id,
            "text": text[:4096],
            "parse_mode": "HTML",
            "disable_web_page_preview": True,
            "reply_markup": keyboard(buttons or []),
        }
        self.call("editMessageText", **params)

    def answer_callback(self, callback_id: str, text: str = "") -> None:
        self.call("answerCallbackQuery", callback_query_id=callback_id, text=text[:200])


def keyboard(rows: list[list[tuple[str, str]]]) -> dict:
    return {"inline_keyboard": [[{"text": t, "callback_data": d} for t, d in row] for row in rows]}


def esc(text: str) -> str:
    return html.escape(text or "", quote=False)
