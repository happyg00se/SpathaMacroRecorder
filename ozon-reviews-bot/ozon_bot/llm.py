"""Ответы через локальную нейросеть (Ollama, LM Studio и любой OpenAI-совместимый сервер)."""
from __future__ import annotations

import json
import re

import requests

from .models import Draft, Item

SENTIMENTS = ("positive", "neutral", "negative")

# Ozon запрещает уводить покупателя с площадки: ссылки, телефоны, мессенджеры, почта.
_FORBIDDEN = re.compile(
    r"https?://|www\.|\S+@\S+\.\w+|(?:\+7|8)[\s(-]*\d{3}[\s)-]*\d{3}[\s-]*\d{2}[\s-]*\d{2}"
    r"|telegram|телеграм|whats\s*app|вотсап|ватсап|viber|вайбер|vk\.com|wildberries|вайлдберриз",
    re.IGNORECASE,
)


class LLMError(Exception):
    pass


def build_system_prompt(cfg: dict) -> str:
    shop = cfg.get("shop") or {}
    rules = "\n".join(f"- {r}" for r in cfg.get("style_rules") or [])
    examples = "\n\n".join(
        f"Покупатель: {e['text']}\nОтвет: {e['reply']}" for e in cfg.get("examples") or []
    )
    return f"""Ты — менеджер магазина «{shop.get('name', 'магазин')}» на Ozon и отвечаешь на отзывы и вопросы покупателей.
О магазине: {shop.get('about', '')}
Как покупателю связаться с нами: {shop.get('contact', 'через чат с продавцом на Ozon')}.

Главное: читай СМЫСЛ текста, а не количество звёзд. Пятёрка с жалобой — это жалоба. Тройка с похвалой — это похвала.
Определи, о чём пишет человек (размер, качество, доставка, упаковка, комплектация, «не подошло», похвала, вопрос о товаре и т.д.), и ответь именно на это.

Правила ответа:
{rules}
- Никогда не пиши ссылки, телефоны, почту, мессенджеры и названия других площадок — Ozon за это наказывает.
- Не выдумывай характеристики товара. Если на вопрос нельзя точно ответить по тексту — вежливо предложи написать в чат продавца и поставь needs_human = true.
- Длина ответа — до {shop.get('max_reply_chars', 900)} символов, 2–4 предложения.
{f"- В конце добавь подпись: {shop['signature']}" if shop.get('signature') else ""}

needs_human = true, если: угрозы, жалоба в суд/Роспотребнадзор, брак с риском для здоровья, оскорбления, подозрение на подделку, спорная ситуация с деньгами, или ты не уверен в ответе.

Примеры ответов в нужном стиле:
{examples}

Верни СТРОГО JSON без пояснений:
{{"sentiment": "positive|neutral|negative", "topic": "коротко, о чём текст", "needs_human": false, "reason": "почему нужен человек или пусто", "reply": "текст ответа покупателю"}}"""


def build_user_prompt(item: Item, hint: str = "") -> str:
    parts = [f"Тип: {'отзыв' if item.kind == 'review' else 'вопрос о товаре'}"]
    if item.product_name:
        parts.append(f"Товар: {item.product_name}")
    if item.rating is not None:
        parts.append(f"Оценка: {item.rating} из 5 (второстепенно, смотри на текст)")
    if item.author:
        parts.append(f"Имя покупателя: {item.author}")
    parts.append(f"Текст покупателя:\n{item.text.strip() or '(текста нет, только оценка)'}")
    if hint:
        parts.append(f"Указание от владельца магазина, обязательно учти: {hint}")
    return "\n".join(parts)


class LocalLLM:
    def __init__(self, cfg: dict, session: requests.Session | None = None):
        self.cfg = cfg
        llm = cfg["llm"]
        self.url = llm["base_url"].rstrip("/") + "/chat/completions"
        self.model = llm["model"]
        self.api_key = llm.get("api_key") or ""
        self.temperature = float(llm.get("temperature", 0.4))
        self.timeout = float(llm.get("timeout_seconds", 300))
        self.json_mode = bool(llm.get("json_mode", True))
        self.max_chars = int((cfg.get("shop") or {}).get("max_reply_chars", 900))
        self.system_prompt = build_system_prompt(cfg)
        self.http = session or requests.Session()
        if session is None and re.search(r"//(localhost|127\.0\.0\.1)[:/]", self.url):
            self.http.trust_env = False  # системный прокси не должен перехватывать локальную нейросеть

    def draft(self, item: Item, hint: str = "") -> Draft:
        body = {
            "model": self.model,
            "temperature": self.temperature,
            "messages": [
                {"role": "system", "content": self.system_prompt},
                {"role": "user", "content": build_user_prompt(item, hint)},
            ],
        }
        if self.json_mode:
            body["response_format"] = {"type": "json_object"}
        headers = {"Authorization": f"Bearer {self.api_key}"} if self.api_key else {}
        try:
            resp = self.http.post(self.url, json=body, headers=headers, timeout=self.timeout)
        except requests.RequestException as e:
            raise LLMError(f"Нейросеть не отвечает ({self.url}): {e}") from e
        if resp.status_code >= 400:
            raise LLMError(f"Нейросеть вернула ошибку {resp.status_code}: {resp.text[:300]}")
        try:
            content = resp.json()["choices"][0]["message"]["content"]
        except (ValueError, KeyError, IndexError) as e:
            raise LLMError(f"Непонятный ответ нейросети: {resp.text[:300]}") from e
        return self.check(parse_draft(content))

    def check(self, draft: Draft) -> Draft:
        """Страховка поверх нейросети: запрещёнка и длина."""
        if _FORBIDDEN.search(draft.reply):
            draft.needs_human = True
            draft.reason = _join(draft.reason, "в ответе ссылка/контакт — Ozon такое не пропустит")
        if len(draft.reply) > self.max_chars:
            draft.reply = _trim(draft.reply, self.max_chars)
        if not draft.reply.strip():
            draft.needs_human = True
            draft.reason = _join(draft.reason, "нейросеть вернула пустой ответ")
        return draft


def parse_draft(content: str) -> Draft:
    """Достаёт JSON из ответа модели. Если модель ответила просто текстом — берём текст, но зовём человека."""
    content = re.sub(r"<think>.*?</think>", "", content, flags=re.DOTALL).strip()
    data = None
    match = re.search(r"\{.*\}", content, re.DOTALL)
    if match:
        try:
            data = json.loads(match.group(0))
        except json.JSONDecodeError:
            data = None
    if not isinstance(data, dict) or not str(data.get("reply", "")).strip():
        text = content.strip().strip("`").strip()
        return Draft(reply=text, needs_human=True, reason="нейросеть ответила не по формату")

    sentiment = str(data.get("sentiment", "neutral")).lower().strip()
    if sentiment not in SENTIMENTS:
        sentiment = "neutral"
    return Draft(
        reply=str(data["reply"]).strip(),
        sentiment=sentiment,
        topic=str(data.get("topic", "")).strip(),
        needs_human=_as_bool(data.get("needs_human", False)),
        reason=str(data.get("reason", "") or "").strip(),
    )


def _as_bool(v) -> bool:
    if isinstance(v, str):
        return v.strip().lower() in ("true", "yes", "да", "1")
    return bool(v)


def _join(a: str, b: str) -> str:
    return f"{a}; {b}" if a else b


def _trim(text: str, limit: int) -> str:
    cut = text[:limit]
    end = max(cut.rfind(". "), cut.rfind("! "), cut.rfind("? "))
    return cut[: end + 1] if end > limit // 2 else cut.rstrip() + "…"
