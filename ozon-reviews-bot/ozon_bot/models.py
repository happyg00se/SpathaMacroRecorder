from __future__ import annotations

from dataclasses import dataclass, field


@dataclass
class Item:
    """Отзыв или вопрос покупателя."""

    kind: str  # "review" | "question"
    id: str
    text: str
    sku: str = ""
    product_name: str = ""
    rating: int | None = None  # только у отзывов
    author: str = ""
    published_at: str = ""
    extra: dict = field(default_factory=dict)

    @property
    def key(self) -> str:
        return f"{self.kind}:{self.id}"

    @property
    def kind_ru(self) -> str:
        return "Отзыв" if self.kind == "review" else "Вопрос"


@dataclass
class Draft:
    """Ответ, который собрала нейросеть."""

    reply: str
    sentiment: str = "neutral"  # positive | neutral | negative
    topic: str = ""
    needs_human: bool = False
    reason: str = ""  # почему нужен человек
