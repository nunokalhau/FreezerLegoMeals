from __future__ import annotations

from dataclasses import dataclass
from threading import Lock
import json
import math
import os
from urllib import parse, request


@dataclass(frozen=True)
class VectorMatch:
    recipe_id: str
    score: float


class IVectorStore:
    def search(self, query_embedding: list[float], top_k: int) -> list[VectorMatch]:
        raise NotImplementedError


@dataclass(frozen=True)
class ChromaVectorStoreOptions:
    base_url: str = "http://localhost:8001"
    tenant: str = "default_tenant"
    database: str = "default_database"
    collection_name: str = "recipe_embeddings"
    timeout_seconds: float = 30.0

    @classmethod
    def from_environment(cls) -> "ChromaVectorStoreOptions":
        return cls(
            base_url=(os.getenv("CHROMA_VECTOR_STORE_BASE_URL") or cls.base_url).rstrip("/"),
            tenant=os.getenv("CHROMA_VECTOR_STORE_TENANT") or cls.tenant,
            database=os.getenv("CHROMA_VECTOR_STORE_DATABASE") or cls.database,
            collection_name=os.getenv("CHROMA_VECTOR_STORE_COLLECTION_NAME") or cls.collection_name,
            timeout_seconds=float(os.getenv("CHROMA_VECTOR_STORE_TIMEOUT_SECONDS", str(cls.timeout_seconds))),
        )


class ChromaVectorStore(IVectorStore):
    def __init__(self, options: ChromaVectorStoreOptions | None = None):
        self.options = options or ChromaVectorStoreOptions.from_environment()
        if not self.options.collection_name.strip():
            raise RuntimeError("ChromaVectorStore collection name must be configured.")
        if not self.options.tenant.strip():
            raise RuntimeError("ChromaVectorStore tenant must be configured.")
        if not self.options.database.strip():
            raise RuntimeError("ChromaVectorStore database must be configured.")

        self._collection_id: str | None = None
        self._collection_lock = Lock()

    def search(self, query_embedding: list[float], top_k: int) -> list[VectorMatch]:
        if top_k <= 0 or len(query_embedding) == 0:
            return []

        collection_id = self._ensure_collection_id()
        payload = {
            "query_embeddings": [query_embedding],
            "n_results": top_k,
            "include": ["embeddings", "distances"],
        }
        query_response = self._post_json(self._build_collection_query_path(collection_id), payload)

        ids_groups = query_response.get("ids") or []
        if len(ids_groups) == 0:
            return []

        ids = ids_groups[0] or []
        if len(ids) == 0:
            return []

        embeddings_groups = query_response.get("embeddings")
        distances_groups = query_response.get("distances")
        embeddings = embeddings_groups[0] if isinstance(embeddings_groups, list) and len(embeddings_groups) > 0 else None
        distances = distances_groups[0] if isinstance(distances_groups, list) and len(distances_groups) > 0 else None

        matches: list[VectorMatch] = []
        for index, recipe_id in enumerate(ids):
            score = 0.0

            # Preserve LocalVectorStore behavior by preferring cosine similarity when vectors are available.
            if isinstance(embeddings, list) and index < len(embeddings) and isinstance(embeddings[index], list) and len(embeddings[index]) > 0:
                score = cosine_similarity(query_embedding, [float(value) for value in embeddings[index]])
            elif isinstance(distances, list) and index < len(distances) and distances[index] is not None:
                score = 1.0 - float(distances[index])

            matches.append(VectorMatch(str(recipe_id), score))

        matches.sort(key=lambda match: match.score, reverse=True)
        return matches[:top_k]

    def _ensure_collection_id(self) -> str:
        if self._collection_id is not None:
            return self._collection_id

        with self._collection_lock:
            if self._collection_id is not None:
                return self._collection_id

            payload = {
                "name": self.options.collection_name,
                "get_or_create": True,
            }
            response = self._post_json(self._build_collections_path(), payload)
            collection_id = response.get("id")
            if not collection_id or not str(collection_id).strip():
                raise RuntimeError("ChromaDB collection creation response did not include an id.")

            self._collection_id = str(collection_id)
            return self._collection_id

    def _build_collections_path(self) -> str:
        return (
            f"/api/v2/tenants/{_escape(self.options.tenant)}/"
            f"databases/{_escape(self.options.database)}/collections"
        )

    def _build_collection_query_path(self, collection_id: str) -> str:
        return f"{self._build_collections_path()}/{_escape(collection_id)}/query"

    def _post_json(self, path: str, payload: dict) -> dict:
        endpoint = f"{self.options.base_url.rstrip('/')}{path}"
        body = json.dumps(payload).encode("utf-8")
        req = request.Request(
            endpoint,
            data=body,
            headers={"Content-Type": "application/json"},
            method="POST",
        )

        with request.urlopen(req, timeout=self.options.timeout_seconds) as response:
            return json.loads(response.read().decode("utf-8") or "{}")


def cosine_similarity(left: list[float], right: list[float]) -> float:
    if not left or not right or len(left) != len(right):
        return 0.0

    dot = sum(a * b for a, b in zip(left, right))
    left_norm = math.sqrt(sum(value * value for value in left))
    right_norm = math.sqrt(sum(value * value for value in right))
    if left_norm == 0 or right_norm == 0:
        return 0.0

    return dot / (left_norm * right_norm)


def _escape(value: str) -> str:
    return parse.quote(value, safe="")