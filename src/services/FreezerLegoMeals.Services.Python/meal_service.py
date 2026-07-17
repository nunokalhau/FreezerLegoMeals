"""
Meal Service for Freezer Lego Meals.
This service layer handles business logic for meal-related operations.
"""

from pathlib import Path
from typing import List, Dict, Any, Optional
from dataclasses import dataclass

# Import Repository from the repository module
from src.repository.FreezerLegoMeals.Repository.Python import Repository


@dataclass
class Recipe:
    """Data class representing a recipe."""
    id: int
    name: str
    source_path: str
    tags: Optional[str] = None
    servings: Optional[int] = None
    time_to_prepare: Optional[str] = None
    prepping: Optional[str] = None
    freezing_notes: Optional[str] = None
    reheat_notes: Optional[str] = None
    combinations: Optional[str] = None
    notes: Optional[str] = None
    matched_ingredients: Optional[List[str]] = None


class MealService:
    """Provides business logic for meal-related operations."""
    
    def __init__(self, db_path: Optional[Path] = None):
        """
        Initialize the Meal Service with database connection.
        
        Args:
            db_path: Path to SQLite database. If None, uses default project location.
        """
        self.repository = Repository(db_path)
    
    def search_recipes_by_ingredients(self, ingredients: List[str]) -> List[Dict[str, Any]]:
        """
        Search for recipes containing any of the specified ingredients.
        
        Args:
            ingredients: List of ingredient names to search for
            
        Returns:
            List of recipe dictionaries with matching ingredients
        """
        return self.repository.search_recipes_by_ingredients(ingredients)
    
    def get_recipe_by_id(self, recipe_id: int) -> Optional[Dict[str, Any]]:
        """
        Search for a specific recipe by ID.
        
        Args:
            recipe_id (int): Recipe ID to search for
            
        Returns:
            Recipe dictionary if found, None otherwise
        """
        return self.repository.get_recipe_by_id(recipe_id)
    
    def find_meals_with_ingredients(self, query: str) -> Dict[str, Any]:
        """
        Parse a natural language query and find meals with specified ingredients.
        
        Args:
            query (str): Natural language query about meals/recipes
            
        Returns:
            Dictionary containing search results and information
        """
        # Extract ingredient words from the query
        # Simple pattern matching for common food terms
        food_terms = [
            "chicken", "beef", "pork", "tofu", "rice", "potato", "carrot", 
            "broccoli", "spinach", "onion", "garlic", "tomato", "bean", 
            "pepper", "cucumber", "mushroom", "egg", "salmon", "lamb",
            "turkey", "duck", "shrimp", "fish", "quinoa", "noodles", "pasta"
        ]
        
        found_ingredients = []
        query_lower = query.lower()
        
        for term in food_terms:
            if term in query_lower:
                found_ingredients.append(term)
                
        # If no ingredients found, try to extract words that might be ingredients
        if not found_ingredients:
            import re
            words = re.findall(r'\b\w+\b', query_lower)
            for word in words:
                if word in food_terms:
                    found_ingredients.append(word)
        
        if found_ingredients:
            # Search for recipes with these ingredients
            recipes = self.search_recipes_by_ingredients(found_ingredients)
            
            return {
                "query": query,
                "search_terms": found_ingredients,
                "total_recipes_found": len(recipes),
                "recipes": recipes,
                "message": f"Found {len(recipes)} recipes containing {', '.join(found_ingredients)}"
            }
        else:
            return {
                "query": query,
                "search_terms": [],
                "total_recipes_found": 0,
                "recipes": [],
                "message": "No ingredients found in your query. Try mentioning specific ingredients like 'chicken', 'beef', etc."
            }
    
    def get_recipe_details(self, recipe_id: int) -> Dict[str, Any]:
        """
        Get detailed information about a specific recipe.
        
        Args:
            recipe_id (int): The recipe ID
            
        Returns:
            Detailed recipe information
        """
        recipe = self.get_recipe_by_id(recipe_id)
        
        if not recipe:
            return {
                "error": f"No recipe found with ID {recipe_id}"
            }
            
        # Add more structured information
        return {
            "query": f"Recipe details for {recipe['name']}",
            "recipe": recipe,
            "message": f"Details for recipe: {recipe['name']}"
        }