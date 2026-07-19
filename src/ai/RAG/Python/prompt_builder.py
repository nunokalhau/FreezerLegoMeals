from __future__ import annotations

from pathlib import Path


DEFAULT_TEMPLATE_PATH = Path(__file__).resolve().parents[1] / "prompts" / "rag_prompt.txt"


class PromptBuilder:
    def __init__(self, template_path: Path | None = None):
        self.template_path = template_path or DEFAULT_TEMPLATE_PATH

    def build(self, question: str, recipes: list) -> str:
        template = self.template_path.read_text(encoding="utf-8")
        return template.replace("{recipes}", self._format_recipes(recipes)).replace("{question}", question.strip())

    def _format_recipes(self, recipes: list) -> str:
        if not recipes:
            return "No relevant recipes were retrieved."

        return "\n\n".join(self._format_recipe(recipe) for recipe in recipes)

    def _format_recipe(self, recipe) -> str:
        ingredients = ", ".join(recipe.ingredients) if recipe.ingredients else "Not specified"
        return "\n".join([
            f"Recipe ID: {recipe.recipeId}",
            f"Title: {recipe.title}",
            f"Description: {recipe.description or 'Not specified'}",
            f"Tags: {recipe.tags or 'Not specified'}",
            f"Ingredients: {ingredients}",
            f"Preparation steps: {recipe.preparationSteps or 'Not specified'}",
            f"Cooking time: {recipe.cookingTime or 'Not specified'}",
            f"Similarity score: {recipe.similarityScore:.6f}",
        ])