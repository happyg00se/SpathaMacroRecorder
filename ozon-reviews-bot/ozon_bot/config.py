"""Загрузка config.yaml. Секреты можно переопределить переменными окружения."""
from __future__ import annotations

import os
from pathlib import Path

import yaml

AUTO_MODES = ("all", "safe", "manual")

_ENV_OVERRIDES = {
    "OZON_CLIENT_ID": ("ozon_api", "client_id"),
    "OZON_API_KEY": ("ozon_api", "api_key"),
    "TELEGRAM_TOKEN": ("telegram", "token"),
    "LLM_BASE_URL": ("llm", "base_url"),
    "LLM_MODEL": ("llm", "model"),
}


class ConfigError(Exception):
    pass


def load_config(path: str | Path = "config.yaml") -> dict:
    path = Path(path)
    if not path.exists():
        raise ConfigError(
            f"Не найден {path}. Скопируй config.example.yaml в config.yaml и заполни его."
        )
    with path.open(encoding="utf-8") as f:
        cfg = yaml.safe_load(f) or {}

    for env, (section, key) in _ENV_OVERRIDES.items():
        if os.environ.get(env):
            cfg.setdefault(section, {})[key] = os.environ[env]

    # Относительные пути считаем от папки с конфигом, а не от места запуска.
    base = path.resolve().parent
    cfg["data_dir"] = str(base / cfg.get("data_dir", "data"))
    browser = cfg.setdefault("browser", {})
    browser["profile_dir"] = str(base / browser.get("profile_dir", "data/chrome-profile"))

    validate(cfg)
    return cfg


def validate(cfg: dict) -> None:
    source = cfg.get("source", "api")
    if source not in ("api", "browser"):
        raise ConfigError("source должен быть api или browser")
    if source == "api":
        api = cfg.get("ozon_api") or {}
        if not api.get("client_id") or not api.get("api_key"):
            raise ConfigError("Для source: api заполни ozon_api.client_id и ozon_api.api_key")
    if cfg.get("auto_publish", "safe") not in AUTO_MODES:
        raise ConfigError(f"auto_publish должен быть одним из: {', '.join(AUTO_MODES)}")
    tg = cfg.get("telegram") or {}
    if not tg.get("token"):
        raise ConfigError("Заполни telegram.token (токен от @BotFather)")
    llm = cfg.get("llm") or {}
    if not llm.get("base_url") or not llm.get("model"):
        raise ConfigError("Заполни llm.base_url и llm.model")
