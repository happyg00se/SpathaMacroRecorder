"""SQLite: какие отзывы уже видели, какие ответы ждут кнопки, настройки бота."""
from __future__ import annotations

import json
import sqlite3
import threading
from dataclasses import asdict
from datetime import datetime
from pathlib import Path

from .models import Draft, Item

# Статусы записи
NEW = "new"
PENDING = "pending"  # ждёт решения в Telegram
PUBLISHED = "published"
SKIPPED = "skipped"
ERROR = "error"


class Storage:
    def __init__(self, path: str | Path):
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        self._db = sqlite3.connect(str(path), check_same_thread=False)
        self._db.row_factory = sqlite3.Row
        self._lock = threading.Lock()
        with self._lock:
            self._db.executescript(
                """
                CREATE TABLE IF NOT EXISTS items (
                    key TEXT PRIMARY KEY,
                    item TEXT NOT NULL,
                    status TEXT NOT NULL,
                    draft TEXT,
                    error TEXT,
                    tg_chat_id INTEGER,
                    tg_message_id INTEGER,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS kv (k TEXT PRIMARY KEY, v TEXT);
                """
            )
            self._db.commit()

    # ─── записи ──────────────────────────────────────────────────────────────
    def has(self, key: str) -> bool:
        with self._lock:
            return self._db.execute("SELECT 1 FROM items WHERE key=?", (key,)).fetchone() is not None

    def add(self, item: Item) -> None:
        now = _now()
        with self._lock:
            self._db.execute(
                "INSERT OR IGNORE INTO items(key,item,status,created_at,updated_at) VALUES(?,?,?,?,?)",
                (item.key, json.dumps(asdict(item), ensure_ascii=False), NEW, now, now),
            )
            self._db.commit()

    def update(self, key: str, **fields) -> None:
        if "draft" in fields and isinstance(fields["draft"], Draft):
            fields["draft"] = json.dumps(asdict(fields["draft"]), ensure_ascii=False)
        fields["updated_at"] = _now()
        cols = ", ".join(f"{k}=?" for k in fields)
        with self._lock:
            self._db.execute(f"UPDATE items SET {cols} WHERE key=?", (*fields.values(), key))
            self._db.commit()

    def get(self, key: str) -> tuple[Item, str, Draft | None, sqlite3.Row] | None:
        with self._lock:
            row = self._db.execute("SELECT * FROM items WHERE key=?", (key,)).fetchone()
        if row is None:
            return None
        item = Item(**json.loads(row["item"]))
        draft = Draft(**json.loads(row["draft"])) if row["draft"] else None
        return item, row["status"], draft, row

    def keys_with_status(self, status: str) -> list[str]:
        with self._lock:
            rows = self._db.execute(
                "SELECT key FROM items WHERE status=? ORDER BY created_at", (status,)
            ).fetchall()
        return [r["key"] for r in rows]

    def counts(self) -> dict[str, int]:
        with self._lock:
            rows = self._db.execute("SELECT status, COUNT(*) c FROM items GROUP BY status").fetchall()
        return {r["status"]: r["c"] for r in rows}

    # ─── настройки ───────────────────────────────────────────────────────────
    def get_kv(self, k: str, default: str | None = None) -> str | None:
        with self._lock:
            row = self._db.execute("SELECT v FROM kv WHERE k=?", (k,)).fetchone()
        return row["v"] if row else default

    def set_kv(self, k: str, v: str) -> None:
        with self._lock:
            self._db.execute("INSERT OR REPLACE INTO kv(k,v) VALUES(?,?)", (k, v))
            self._db.commit()


def _now() -> str:
    return datetime.now().isoformat(timespec="seconds")
