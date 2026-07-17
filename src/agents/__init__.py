"""
Agents layer for Freezer Lego Meals.
This layer understands user requests, coordinates tools, combines results,
and never accesses databases or files directly.
"""

from src.tools import RecipeSearchTool, IndexGenerationTool, ValidationTool
from typing import List, Dict, Any, Optional


class MealAgent:
    """Handles recipe-related queries and requests."""
    
    def __init__(self):
        self.search_tool = RecipeSearchTool()
        self.index_tool = IndexGenerationTool()
        self.validation_tool = ValidationTool()
    
    def find_meals_with_ingredients(self, query: str) -> Dict[str, Any]:
        """Parse a natural language query and find meals with specified ingredients."""
        # For simplicity, we'll extract a few key patterns for this example
        # In the future this could be more sophisticated NLP
        ingredients = self._extract_ingredients_from_query(query)
        
        if ingredients:
            results = self.search_tool.search_by_ingredients(ingredients)
            
            return {
                "query": query,
                "search_terms": ingredients,
                "total_recipes_found": len(results['recipes']),
                "recipes": results['recipes'],
                "message": f"Found {len(results['recipes'])} recipes containing {', '.join(ingredients)}"
            }
        else:
            return {
                "query": query,
                "search_terms": [],
                "total_recipes_found": 0,
                "recipes": [],
                "message": "No ingredients found in your query. Try mentioning specific ingredients."
            }
    
    def get_recipe_details(self, recipe_id: int) -> Dict[str, Any]:
        """Get detailed information about a specific recipe."""
        recipe = self.search_tool.get_recipe_by_id(recipe_id)
        
        if not recipe:
            return {
                "error": f"No recipe found with ID {recipe_id}"
            }
            
        return {
            "query": f"Recipe details for {recipe['name']}",
            "recipe": recipe,
            "message": f"Details for recipe: {recipe['name']}"
        }
    
    def search_recipes_by_ingredients(self, ingredients: List[str]) -> List[Dict[str, Any]]:
        """
        Search for recipes containing specific ingredients.
        
        Args:
            ingredients: List of ingredient names to search for
            
        Returns:
            List of matching recipes
        """
        return self.search_tool.search_by_ingredients(ingredients)
    
    def _extract_ingredients_from_query(self, query: str) -> List[str]:
        """Extract potential ingredient words from a natural language query."""
        # This is a simplified implementation - could be enhanced with NLP
        food_terms = [
            "chicken", "beef", "pork", "tofu", "rice", "potato", "carrot", 
            "broccoli", "spinach", "onion", "garlic", "tomato", "bean", 
            "pepper", "cucumber", "mushroom", "egg", "salmon", "lamb",
            "turkey", "duck", "shrimp", "fish", "quinoa", "noodles", "pasta",
            "cabbage", "cauliflower", "bell pepper", "zucchini", "asparagus",
            "avocado", "banana", "apple", "orange", "grape", "strawberry"
        ]
        
        found_ingredients = []
        query_lower = query.lower()
        
        for term in food_terms:
            if term in query_lower:
                found_ingredients.append(term)
                
        return found_ingredients


class ShoppingAgent:
    """Handles shopping list generation and management."""
    
    def __init__(self):
        self.search_tool = RecipeSearchTool()
        self.index_tool = IndexGenerationTool()
        self.validation_tool = ValidationTool()
    
    def generate_shopping_list(self, recipe_names: List[str], **kwargs) -> Dict[str, Any]:
        """This is an example implementation - would be more complex in reality."""
        # For demonstration purposes
        return {
            "status": "ready",
            "recipe_names": recipe_names,
            "message": f"Generated shopping list for {len(recipe_names)} recipes"
        }