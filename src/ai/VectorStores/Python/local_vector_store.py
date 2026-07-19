from __future__ import annotations

from abc import ABC, abstractmethod
from dataclasses import dataclass
import json
import math
from pathlib import Path


@dataclass(frozen=True)
class VectorMatch:
    recipe_id: str
    score: float


class IVectorStore(ABC):
    @abstractmethod
    def search(self, query_embedding: list[float], top_k: int) -> list[VectorMatch]:
        raise NotImplementedError


class LocalVectorStore(IVectorStore):
    def __init__(self, embeddings_dir: Path):
        self.embeddings_dir = embeddings_dir
        self._cache: list[tuple[str, list[float]]] | None = None

    def search(self, query_embedding: list[float], top_k: int) -> list[VectorMatch]:
        if top_k <= 0:
            return []

        embeddings = self._load_embeddings_once()
        matches = [
            VectorMatch(recipe_id=recipe_id, score=cosine_similarity(query_embedding, embedding))
            for recipe_id, embedding in embeddings
        ]
        matches.sort(key=lambda match: match.score, reverse=True)
        return matches[:top_k]

    def _load_embeddings_once(self) -> list[tuple[str, list[float]]]:
        if self._cache is not None:
            return self._cache

        if not self.embeddings_dir.exists():
            self._cache = []
            return self._cache

        loaded: list[tuple[str, list[float]]] = []
        for path in sorted(self.embeddings_dir.glob("*.json")):
            payload = json.loads(path.read_text(encoding="utf-8"))
            recipe_id = str(payload.get("recipeId") or path.stem)
            embedding = payload.get("embedding") or []
            if isinstance(embedding, list) and embedding:
                loaded.append((recipe_id, [float(value) for value in embedding]))

        # TODO: Move this cache to Redis if local process memory becomes insufficient.
        self._cache = loaded
        return self._cache


def cosine_similarity(left: list[float], right: list[float]) -> float:
    if not left or not right or len(left) != len(right):
        return 0.0

    dot = sum(a * b for a, b in zip(left, right))
    left_norm = math.sqrt(sum(value * value for value in left))
    right_norm = math.sqrt(sum(value * value for value in right))
    if left_norm == 0 or right_norm == 0:
        return 0.0

    return dot / (left_norm * right_norm)