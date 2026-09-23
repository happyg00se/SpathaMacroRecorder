"""Запуск: python -m ozon_bot [run|login|diagnose|dry-run|try "текст отзыва"] [--config config.yaml]"""
from __future__ import annotations

import argparse
import logging
import sys
from pathlib import Path

from .config import ConfigError, load_config


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="ozon_bot", description="Ответы на отзывы Ozon локальной нейросетью")
    parser.add_argument(
        "command",
        nargs="?",
        default="run",
        choices=["run", "login", "diagnose", "dry-run", "try"],
        help="run — бот; login — войти в кабинет Ozon в Chrome; diagnose — снять страницу кабинета для настройки; "
        "dry-run — показать ответы на текущие отзывы без публикации; try — ответ на свой текст",
    )
    parser.add_argument("text", nargs="?", default="", help="текст отзыва для команды try")
    parser.add_argument("--rating", type=int, default=None, help="оценка для команды try")
    parser.add_argument("--config", default="config.yaml")
    args = parser.parse_args(argv)

    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")  # чтобы кириллица не ломалась в консоли Windows
    try:
        cfg = load_config(args.config)
    except ConfigError as e:
        print(f"Ошибка настройки: {e}")
        return 2

    Path(cfg["data_dir"]).mkdir(parents=True, exist_ok=True)
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
        handlers=[
            logging.StreamHandler(),
            logging.FileHandler(Path(cfg["data_dir"]) / "bot.log", encoding="utf-8"),
        ],
    )
    logging.getLogger("urllib3").setLevel(logging.WARNING)

    if args.command in ("login", "diagnose"):
        from .ozon_browser import OzonBrowser

        browser = OzonBrowser(cfg)
        if args.command == "login":
            browser.login()
            print("Готово: вход сохранён в профиле бота.")
        else:
            print(f"Сохранено в {browser.diagnose()}")
        return 0

    from .llm import LocalLLM
    from .models import Item

    if args.command == "try":
        item = Item(kind="review", id="try", text=args.text, rating=args.rating)
        draft = LocalLLM(cfg).draft(item)
        print(f"Тон: {draft.sentiment} | Тема: {draft.topic} | Нужен человек: {draft.needs_human} {draft.reason}")
        print(f"\n{draft.reply}")
        return 0

    from .app import build

    app = build(cfg)
    if args.command == "dry-run":
        items = app.source.fetch_new()
        print(f"Найдено: {len(items)}\n")
        for item in items:
            draft = app.llm.draft(item)
            auto = "сам" if app.should_autopublish(item, draft) else "на кнопки"
            print(f"── {item.kind_ru} {item.id} · {item.rating or ''}★ · {item.product_name}")
            print(f"   {item.text.strip()[:300] or '(без текста)'}")
            print(f"   [{draft.sentiment}, {draft.topic}, {auto}] {draft.reply}\n")
        return 0

    app.run()
    return 0


if __name__ == "__main__":
    sys.exit(main())
