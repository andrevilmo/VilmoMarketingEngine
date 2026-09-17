from __future__ import annotations

import json
import re
from pathlib import Path

from agent.app.config import get_settings


def _tokenize(text: str) -> set[str]:
    return {t for t in re.findall(r"[a-zA-Z0-9À-ÿ_]{3,}", text.lower())}


def load_chunks() -> list[dict[str, str]]:
    settings = get_settings()
    root = Path(settings.knowledge_dir)
    chunks: list[dict[str, str]] = []
    if not root.exists():
        return chunks
    for path in sorted(root.glob("**/*")):
        if path.suffix.lower() not in {".md", ".txt", ".json"}:
            continue
        text = path.read_text(encoding="utf-8")
        # Simple chunking by paragraphs (~800 chars)
        buf = ""
        for para in text.split("\n\n"):
            if len(buf) + len(para) > 800 and buf:
                chunks.append({"source": str(path), "text": buf.strip()})
                buf = para
            else:
                buf = f"{buf}\n\n{para}".strip()
        if buf.strip():
            chunks.append({"source": str(path), "text": buf.strip()})
    return chunks


def retrieve(query: str, top_k: int | None = None) -> list[str]:
    """Lightweight lexical RAG over local knowledge base."""
    settings = get_settings()
    k = top_k or settings.rag_top_k
    q = _tokenize(query)
    scored: list[tuple[int, str]] = []
    for chunk in load_chunks():
        tokens = _tokenize(chunk["text"])
        score = len(q & tokens)
        if score:
            scored.append((score, f"[{chunk['source']}] {chunk['text'][:400]}"))
    scored.sort(key=lambda x: x[0], reverse=True)
    return [t for _, t in scored[:k]]


def append_thread_memory(thread_id: str, record: dict) -> None:
    settings = get_settings()
    path = Path(settings.memory_dir) / f"{thread_id}.jsonl"
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a", encoding="utf-8") as fh:
        fh.write(json.dumps(record, ensure_ascii=False) + "\n")


def read_thread_memory(thread_id: str, limit: int = 5) -> list[dict]:
    settings = get_settings()
    path = Path(settings.memory_dir) / f"{thread_id}.jsonl"
    if not path.exists():
        return []
    lines = path.read_text(encoding="utf-8").strip().splitlines()
    out: list[dict] = []
    for line in lines[-limit:]:
        try:
            out.append(json.loads(line))
        except json.JSONDecodeError:
            continue
    return out
