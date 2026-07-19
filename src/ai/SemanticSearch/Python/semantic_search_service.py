from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Protocol


@dataclass(frozen=True)
class SemanticSearchResult:
    recipeId: str
    title: str
    score: float
    matchedText: str
    reason: str


class EmbeddingService(Protocol):
    def generate_embedding(self, text: str): ...


class VectorStore(Protocol):
    def search(self, query_embedding: list[float], top_k: int): ...


class RecipeMetadataProvider:
    def __init__(self, repository):
        self.repository = repository
        self._cache: dict[str, dict[str, Any]] | None = None

    def get_metadata(self, recipe_id: str) -> dict[str, Any]:
        if self._cache is None:
            self._cache = {}
            for recipe in self.repository.get_all_recipes_with_details():
                key = str(recipe.get("id"))
                recipe["recipe_ingredients"] = self.repository.get_recipe_ingredients(recipe.get("id"))
                self._cache[key] = recipe

        return self._cache.get(str(recipe_id), {"id": recipe_id, "name": f"Recipe {recipe_id}"})


class SemanticSearchService:
    def __init__(self, embedding_service: EmbeddingService, vector_store: VectorStore, metadata_provider: RecipeMetadataProvider):
        self.embedding_service = embedding_service
        self.vector_store = vector_store
        self.metadata_provider = metadata_provider

    def search(self, query: str, top_k: int = 5) -> list[SemanticSearchResult]:
        if not query or not query.strip() or top_k <= 0:
            return []

        query_embedding = self.embedding_service.generate_embedding(query).embedding
        matches = self.vector_store.search(query_embedding, top_k)
        results: list[SemanticSearchResult] = []
        for match in matches:
            metadata = self.metadata_provider.get_metadata(match.recipe_id)
            title = str(metadata.get("name") or metadata.get("title") or f"Recipe {match.recipe_id}")
            matched_text = self._matched_text(metadata)
            results.append(SemanticSearchResult(
                recipeId=str(match.recipe_id),
                title=title,
                score=round(float(match.score), 6),
                matchedText=matched_text,
                reason=f"High semantic similarity between the query and {title}.",
            ))

        return results

    def _matched_text(self, recipe: dict[str, Any]) -> str:
        parts = [
            recipe.get("name"),
            recipe.get("notes"),
            recipe.get("tags"),
            recipe.get("prepping"),
        ]
        ingredients = recipe.get("recipe_ingredients") or []
        ingredient_names = [ingredient.get("name") for ingredient in ingredients if ingredient.get("name")]
        if ingredient_names:
            parts.append(", ".join(ingredient_names))

        return " | ".join(str(part) for part in parts if part not in (None, "", []))