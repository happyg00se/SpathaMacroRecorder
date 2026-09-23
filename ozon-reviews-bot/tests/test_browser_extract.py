from ozon_bot.ozon_browser import extract_items


def test_extract_reviews_from_cabinet_json():
    payload = {"result": {"reviews": [
        {"uuid": "a-1", "text": "Не подошли", "rating": 2, "product": {"title": "Наклейки", "sku": 7}, "author": {"name": "Иван"}},
        {"uuid": "a-2", "text": "Отлично", "rating": 5, "is_answered": True},
        {"id": 9, "text": "меню", "href": "/x"},  # без оценки — не отзыв
    ]}}
    items = extract_items(payload, "review")
    assert [i.id for i in items] == ["a-1", "a-2"]
    assert items[0].product_name == "Наклейки" and items[0].sku == "7" and items[0].author == "Иван"
    assert items[1].extra["answered"] is True


def test_extract_questions():
    items = extract_items({"questions": [{"id": 5, "text": "Есть больше?", "answers_count": 0}]}, "question")
    assert items[0].id == "5" and not items[0].extra["answered"]
