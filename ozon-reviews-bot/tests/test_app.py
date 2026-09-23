from ozon_bot import storage as st
from ozon_bot.app import App
from ozon_bot.llm import LLMError
from ozon_bot.models import Draft, Item
from ozon_bot.storage import Storage


class Source:
    def __init__(self, items):
        self.items = items
        self.published = []
        self.fail = False

    def fetch_new(self):
        return list(self.items)

    def publish(self, item, text):
        if self.fail:
            raise RuntimeError("кабинет недоступен")
        self.published.append((item.key, text))


class LLM:
    def __init__(self, drafts):
        self.drafts = drafts
        self.hints = []

    def draft(self, item, hint=""):
        self.hints.append(hint)
        d = self.drafts[item.id]
        if isinstance(d, Exception):
            raise d
        return Draft(**d.__dict__)


class TG:
    def __init__(self):
        self.sent = []
        self.edits = []

    def send(self, chat, text, buttons=None):
        self.sent.append((chat, text, buttons))
        return len(self.sent)

    def edit(self, chat, mid, text, buttons=None):
        self.edits.append((chat, mid, text))

    def answer_callback(self, cid, text=""):
        pass


def make(tmp_path, items, drafts, mode="safe"):
    cfg = {"telegram": {"allowed_chat_ids": [42]}, "auto_publish": mode}
    app = App(cfg, Source(items), LLM(drafts), TG(), Storage(tmp_path / "db.sqlite3"))
    return app


def run_jobs(app):
    while not app.jobs.empty():
        app.jobs.get()()


POS = Draft(reply="Спасибо!", sentiment="positive")
NEG = Draft(reply="Нам очень жаль, напишите в чат.", sentiment="negative", topic="качество")


def test_safe_mode_publishes_positive_and_asks_on_negative(tmp_path):
    # пятёрка с жалобой: смысл негативный — значит на кнопки, несмотря на звёзды
    items = [Item("review", "1", "Отличные!", rating=5), Item("review", "2", "Клеятся плохо", rating=5)]
    app = make(tmp_path, items, {"1": POS, "2": NEG})
    stats = app.check()
    assert stats["published"] == 1 and stats["pending"] == 1
    assert app.source.published == [("review:1", "Спасибо!")]
    assert app.db.get("review:2")[1] == st.PENDING
    assert app.check()["new"] == 0  # повторно не трогаем


def test_button_publish_and_custom_text(tmp_path):
    app = make(tmp_path, [Item("review", "2", "плохо", rating=1)], {"2": NEG}, mode="manual")
    app.check()
    app.on_button("ok|review:2", 42, 7, "cb")
    run_jobs(app)
    assert app.source.published == [("review:2", NEG.reply)]
    assert app.db.get("review:2")[1] == st.PUBLISHED


def test_edit_with_hint_regenerates(tmp_path):
    app = make(tmp_path, [Item("review", "2", "плохо", rating=1)], {"2": NEG}, mode="manual")
    app.check()
    app.on_button("ed|review:2", 42, 7, "cb")
    app.on_message(42, "? попроси фото")
    run_jobs(app)
    assert app.llm.hints[-1] == "попроси фото"
    app.on_button("ed|review:2", 42, 7, "cb")
    app.on_message(42, "Мой ответ")
    run_jobs(app)
    assert app.source.published == [("review:2", "Мой ответ")]


def test_publish_failure_marks_error(tmp_path):
    app = make(tmp_path, [Item("review", "1", "класс", rating=5)], {"1": POS}, mode="all")
    app.source.fail = True
    assert app.check()["errors"] == 1
    assert app.db.get("review:1")[1] == st.ERROR


def test_llm_failure_is_retried_next_check(tmp_path):
    app = make(tmp_path, [Item("review", "1", "класс", rating=5)], {"1": LLMError("нет связи")}, mode="all")
    assert app.check()["errors"] == 1
    app.llm.drafts["1"] = POS
    assert app.check()["published"] == 1


def test_empty_review_uses_stars_and_needs_human_blocks_all_mode(tmp_path):
    app = make(tmp_path, [], {})
    assert app.should_autopublish(Item("review", "1", "", rating=5), POS)
    assert not app.should_autopublish(Item("review", "1", "", rating=2), POS)
    app.db.set_kv("mode", "all")
    assert not app.should_autopublish(Item("review", "1", "x"), Draft(reply="x", needs_human=True))


def test_stranger_gets_chat_id_only(tmp_path):
    app = make(tmp_path, [], {})
    app.handle_update({"update_id": 1, "message": {"chat": {"id": 99}, "text": "/start"}})
    assert "99" in app.tg.sent[0][1] and app.tg.sent[0][0] == 99
    app.handle_update({"update_id": 2, "message": {"chat": {"id": 99}, "text": "/check"}})
    assert len(app.tg.sent) == 1 and app.jobs.empty()
