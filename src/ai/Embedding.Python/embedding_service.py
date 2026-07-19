from __future__ import annotations

from abc import ABC, abstractmethod
from dataclasses import dataclass
import json
import os
from urllib import request


@dataclass(frozen=True)
class EmbeddingResponse:
    model: str
    dimensions: int
    embedding: list[float]


class IEmbeddingService(ABC):
    @abstractmethod
    def generate_embedding(self, text: str) -> EmbeddingResponse:
        raise NotImplementedError


class OllamaEmbeddingService(IEmbeddingService):
    def __init__(self, base_url: str | None = None, model: str | None = None, timeout_seconds: float | None = None):
        self.base_url = (base_url or os.getenv("OLLAMA_BASE_URL") or "http://localhost:11434").rstrip("/")
        self.model = model or os.getenv("OLLAMA_EMBEDDING_MODEL") or "nomic-embed-text"
        self.timeout_seconds = timeout_seconds or float(os.getenv("OLLAMA_EMBEDDING_TIMEOUT", "60"))

    def generate_embedding(self, text: str) -> EmbeddingResponse:
        if text is None or not text.strip():
            raise ValueError("Text is required to generate an embedding")

        payload = json.dumps({"model": self.model, "prompt": text}).encode("utf-8")
        ollama_request = request.Request(
            f"{self.base_url}/api/embeddings",
            data=payload,
            headers={"Content-Type": "application/json"},
            method="POST",
        )

        with request.urlopen(ollama_request, timeout=self.timeout_seconds) as response:
            response_payload = json.loads(response.read().decode("utf-8"))

        embedding = response_payload.get("embedding")
        if not isinstance(embedding, list) or not embedding:
            raise RuntimeError("Ollama did not return an embedding vector")

        vector = [float(value) for value in embedding]
        return EmbeddingResponse(model=self.model, dimensions=len(vector), embedding=vector)