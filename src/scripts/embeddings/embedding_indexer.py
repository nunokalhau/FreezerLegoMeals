from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Protocol

from recipe_embedding_builder import RecipeEmbeddingBuilder


class EmbeddingService(Protocol):
    def generate_embedding(self, text: str): ...


class EmbeddingIndexer:
    def __init__(self, embedding_service: EmbeddingService, builder: RecipeEmbeddingBuilder | None = None):
        self.embedding_service = embedding_service
        self.builder = builder or RecipeEmbeddingBuilder()

    def index_recipes(self, recipes: list[dict[str, Any]], output_dir: Path) -> int:
        output_dir.mkdir(parents=True, exist_ok=True)
        count = 0

        for recipe in recipes:
            recipe_id = recipe.get("id")
            if recipe_id is None:
                continue

            document = self.builder.build_document(recipe)
            embedding = self.embedding_service.generate_embedding(document)
            payload = {
                "recipeId": str(recipe_id),
                "model": embedding.model,
                "dimensions": embedding.dimensions,
                "embedding": embedding.embedding,
            }

            (output_dir / f"{recipe_id}.json").write_text(json.dumps(payload, indent=2), encoding="utf-8")
            count += 1

        return count