import json

from ozon_bot.llm import LocalLLM, build_system_prompt, build_user_prompt, parse_draft
from ozon_bot.models import Item

CFG = {
    "llm": {"base_url": "http://localhost:11434/v1", "model": "m"},
    "shop": {"name": "Тест", "max_reply_chars": 120, "contact": "чат"},
    "style_rules": ["Будь вежлив"],
    "examples": [{"text": "Не подошли", "reply": "Жаль"}],
}


class FakeResp:
    def __init__(self, content, status=200):
        self.status_code = status
        self._content = content
        self.text = content

    def json(self):
        return {"choices": [{"message": {"content": self._content}}]}


class FakeSession:
    def __init__(self, content):
        self.content = content
        self.sent = None

    def post(self, url, json=None, headers=None, timeout=None):
        self.sent = (url, json)
        return FakeResp(self.content)


def test_parse_json_with_noise():
    d = parse_draft('<think>хм</think>Вот:\n```json\n{"sentiment":"negative","topic":"размер","needs_human":"false","reply":"Жаль!"}\n```')
    assert d.sentiment == "negative" and d.topic == "размер" and d.reply == "Жаль!" and not d.needs_human


def test_parse_plain_text_asks_human():
    d = parse_draft("Здравствуйте! Спасибо.")
    assert d.reply == "Здравствуйте! Спасибо." and d.needs_human


def test_unknown_sentiment_becomes_neutral():
    assert parse_draft('{"sentiment":"злой","reply":"x"}').sentiment == "neutral"


def test_prompt_contains_rules_and_examples():
    p = build_system_prompt(CFG)
    assert "Будь вежлив" in p and "Не подошли" in p and "СМЫСЛ" in p


def test_user_prompt_marks_rating_secondary():
    p = build_user_prompt(Item(kind="review", id="1", text="плохо клеится", rating=5, product_name="Наклейки"))
    assert "второстепенно" in p and "Наклейки" in p and "плохо клеится" in p


def test_draft_request_and_guards():
    reply = {"sentiment": "negative", "topic": "качество", "needs_human": False,
             "reply": "Напишите нам в telegram @shop. " + "Очень жаль. " * 20}
    s = FakeSession(json.dumps(reply, ensure_ascii=False))
    d = LocalLLM(CFG, session=s).draft(Item(kind="review", id="1", text="плохо"))
    url, body = s.sent
    assert url.endswith("/v1/chat/completions") and body["response_format"] == {"type": "json_object"}
    assert d.needs_human and "контакт" in d.reason
    assert len(d.reply) <= 121
