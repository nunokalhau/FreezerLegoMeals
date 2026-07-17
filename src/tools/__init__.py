from src.repository import Repository
from typing import List, Dict, Any, Optional


class RecipeSearchTool:
    def __init__(self):
        self.repo = Repository()
    
    def search_by_ingredients(self, ingredients: List[str]) -> Dict[str, Any]:
        results = self.repo.search_recipes_by_ingredients(ingredients)
        return {
            "search_terms": ingredients,
            "total_recipes_found": len(results),
            "recipes": results
        }
        
    def get_recipe_by_id(self, recipe_id: int) -> Optional[Dict[str, Any]]:
        return self.repo.get_recipe_by_id(recipe_id)


class IndexGenerationTool:
    def __init__(self):
        self.repo = Repository()
    
    def generate_index(self) -> List[Dict[str, Any]]:
        return self.repo.get_all_recipes()


class ValidationTool:
    def __init__(self):
        self.repo = Repository()
    
    def validate_recipe(self, recipe_id: int) -> Dict[str, Any]:
        recipe = self.repo.get_recipe_by_id(recipe_id)
        return {
            "recipe_id": recipe_id,
            "valid": recipe is not None,
            "error": None if recipe else "Recipe not found"
        }