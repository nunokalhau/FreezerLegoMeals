from __future__ import annotations

from typing import Any


class RecipeEmbeddingBuilder:
    def build_document(self, recipe: dict[str, Any]) -> str:
        ingredients = self._format_ingredients(recipe.get("recipe_ingredients") or recipe.get("recipeIngredients") or [])
        sections = [
            ("Title", recipe.get("name") or recipe.get("title")),
            ("Description", recipe.get("notes") or recipe.get("description")),
            ("Tags", self._format_list(recipe.get("tags"))),
            ("Ingredients", ingredients),
            ("Preparation", recipe.get("prepping") or recipe.get("preparation")),
            ("Cooking time", recipe.get("time_to_prepare") or recipe.get("timeToPrepare")),
            ("Instructions", self._join_values([recipe.get("freezing_notes"), recipe.get("reheat_notes"), self._format_list(recipe.get("combinations"))])),
        ]

        return "\n".join(f"{label}: {value}" for label, value in sections if value not in (None, "", []))

    def _format_ingredients(self, ingredients: list[dict[str, Any]]) -> str:
        values: list[str] = []
        for ingredient in ingredients:
            ingredient_data = ingredient.get("ingredient") or {}
            name = ingredient.get("name") or ingredient_data.get("name")
            amount = ingredient.get("amount")
            unit = ingredient.get("unit")
            values.append(" ".join(str(part) for part in [amount, unit, name] if part not in (None, "")))

        return ", ".join(value for value in values if value)

    def _format_list(self, value: Any) -> str:
        if isinstance(value, list):
            return ", ".join(str(item) for item in value if item not in (None, ""))
        return "" if value is None else str(value)

    def _join_values(self, values: list[Any]) -> str:
        return "\n".join(str(value) for value in values if value not in (None, ""))