from __future__ import annotations

from dataclasses import dataclass
from typing import Any


@dataclass(frozen=True)
class SourceAttribution:
    recipeId: str
    title: str
    similarityScore: float


@dataclass(frozen=True)
class RetrievalRecipe:
    recipeId: str
    title: str
    description: str
    tags: str
    ingredients: list[str]
    preparationSteps: str
    cookingTime: str
    similarityScore: float


@dataclass(frozen=True)
class RetrievalResult:
    question: str
    recipes: list[RetrievalRecipe]
    sources: list[SourceAttribution]


class RetrievalService:
    def __init__(self, semantic_search_service, metadata_provider, top_k: int = 3, minimum_similarity: float = 0.2):
        self.semantic_search_service = semantic_search_service
        self.metadata_provider = metadata_provider
        self.top_k = top_k
        self.minimum_similarity = minimum_similarity

    def retrieve(self, question: str) -> RetrievalResult:
        if not question or not question.strip():
            return RetrievalResult(question=question, recipes=[], sources=[])

        matches = self.semantic_search_service.search(question, self.top_k)
        recipes: list[RetrievalRecipe] = []
        for match in matches:
            if float(match.score) < self.minimum_similarity:
                continue

            metadata = self.metadata_provider.get_metadata(match.recipeId)
            recipes.append(self._to_retrieval_recipe(match, metadata))

        return RetrievalResult(
            question=question,
            recipes=recipes,
            sources=[SourceAttribution(recipe.recipeId, recipe.title, recipe.similarityScore) for recipe in recipes],
        )

    def _to_retrieval_recipe(self, match, metadata: dict[str, Any]) -> RetrievalRecipe:
        ingredients = metadata.get("recipe_ingredients") or []
        ingredient_names = [ingredient.get("name") for ingredient in ingredients if ingredient.get("name")]
        return RetrievalRecipe(
            recipeId=str(match.recipeId),
            title=str(metadata.get("name") or metadata.get("title") or match.title),
            description=str(metadata.get("notes") or metadata.get("description") or ""),
            tags=self._format_tags(metadata.get("tags")),
            ingredients=ingredient_names,
            preparationSteps=str(metadata.get("prepping") or metadata.get("preparation") or ""),
            cookingTime=str(metadata.get("time_to_prepare") or metadata.get("timeToPrepare") or ""),
            similarityScore=round(float(match.score), 6),
        )

    def _format_tags(self, tags: Any) -> str:
        if isinstance(tags, list):
            return ", ".join(str(tag) for tag in tags if tag)
        return "" if tags is None else str(tags)