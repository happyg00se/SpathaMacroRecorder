from ozon_bot.ozon_api import OzonApi, OzonError
from ozon_bot.models import Item


class Resp:
    def __init__(self, data, status=200):
        self.status_code = status
        self._d = data
        self.text = str(data)
        self.content = b"x"

    def json(self):
        return self._d


class Session:
    def __init__(self, routes):
        self.routes = routes
        self.headers = {}
        self.calls = []

    def post(self, url, json=None, timeout=None):
        path = url.replace("https://api-seller.ozon.ru", "")
        self.calls.append((path, json))
        r = self.routes[path]
        return r(json) if callable(r) else r


CFG = {"ozon_api": {"client_id": 1, "api_key": "k"}, "max_items_per_check": 30}


def test_fetch_reviews_and_questions_with_names():
    s = Session({
        "/v1/review/list": Resp({"reviews": [{"id": "r1", "text": "Супер", "sku": 55, "rating": 5}], "has_next": False, "last_id": "r1"}),
        "/v1/question/list": Resp({"questions": [{"id": "q1", "text": "Какой размер?", "sku": 55, "author_name": "Аня"}], "last_id": ""}),
        "/v3/product/info/list": Resp({"items": [{"name": "Наклейки", "sources": [{"sku": 55}]}]}),
    })
    items = OzonApi(CFG, session=s).fetch_new()
    assert [i.key for i in items] == ["review:r1", "question:q1"]
    assert all(i.product_name == "Наклейки" for i in items)
    assert s.headers == {"Client-Id": "1", "Api-Key": "k"}


def test_403_reports_premium():
    s = Session({"/v1/review/list": Resp({}, 403), "/v1/question/list": Resp({}, 403)})
    try:
        OzonApi(CFG, session=s).fetch_new()
    except OzonError as e:
        assert "Premium" in str(e)
    else:
        raise AssertionError("ожидалась ошибка")


def test_publish_endpoints():
    s = Session({"/v1/review/comment/create": Resp({}), "/v1/question/answer/create": Resp({})})
    api = OzonApi(CFG, session=s)
    api.publish(Item(kind="review", id="r1", text=""), "Спасибо")
    api.publish(Item(kind="question", id="q1", text="", sku="55"), "Ответ")
    assert s.calls[0] == ("/v1/review/comment/create", {"review_id": "r1", "text": "Спасибо", "mark_review_as_processed": True})
    assert s.calls[1] == ("/v1/question/answer/create", {"question_id": "q1", "sku": 55, "text": "Ответ"})
