"""
Meal Agent for Freezer Lego Meals project.

This agent answers questions about recipes and meals using the project's own data.
It can find recipes with specific ingredients, return recipe details, and provide meal information.
"""

import sqlite3
import json
from pathlib import Path
from typing import List, Dict, Any, Optional
import re

class MealAgent:
    def __init__(self, db_path: Optional[Path] = None):
        """
        Initialize the Meal Agent with database connection.
        
        Args:
            db_path: Path to SQLite database. If None, uses default project location.
        """
        if db_path is None:
            # Default to project's data directory
            self.db_path = Path(__file__).resolve().parents[2] / "data" / "recipes_local.db"
        else:
            self.db_path = db_path
            
        # Validate database exists
        if not self.db_path.exists():
            raise FileNotFoundError(f"Database not found at {self.db_path}")
    
    def search_recipes_by_ingredients(self, ingredients: List[str]) -> List[Dict[str, Any]]:
        """
        Search for recipes containing any of the specified ingredients.
        
        Args:
            ingredients: List of ingredient names to search for
            
        Returns:
            List of recipe dictionaries with matching ingredients
        """
        conn = sqlite3.connect(self.db_path)
        
        try:
            # Build the query with OR logic for ingredients
            ingredient_placeholders = ", ".join(["?" for _ in ingredients])
            query = f"""
                SELECT DISTINCT r.id, r.name, r.source_path, 
                       GROUP_CONCAT(i.name) as matched_ingredients
                FROM recipes r
                JOIN recipe_ingredients ri ON r.id = ri.recipe_id
                JOIN ingredients i ON ri.ingredient_id = i.id
                WHERE i.name IN ({ingredient_placeholders})
                GROUP BY r.id, r.name, r.source_path
                ORDER BY r.name
            """
            
            cursor = conn.execute(query, ingredients)
            recipes = []
            
            for row in cursor.fetchall():
                recipe_id, name, source_path, matched_ingredients_str = row
                
                # Split the matched ingredients if they exist
                matched_ingredients = matched_ingredients_str.split(",") if matched_ingredients_str else []
                
                recipes.append({
                    "id": recipe_id,
                    "name": name,
                    "source_path": source_path,
                    "matched_ingredients": matched_ingredients
                })
            
            return recipes
        
        finally:
            conn.close()
    
    def search_recipe_by_id(self, recipe_id: int) -> Optional[Dict[str, Any]]:
        """
        Search for a specific recipe by ID.
        
        Args:
            recipe_id (int): Recipe ID to search for
            
        Returns:
            Recipe dictionary if found, None otherwise
        """
        conn = sqlite3.connect(self.db_path)
        
        try:
            query = """
                SELECT r.id, r.name, r.source_path, r.tags, r.servings, 
                       r.time_to_prepare, r.prepping, r.freezing_notes, 
                       r.reheat_notes, r.combinations, r.notes
                FROM recipes r
                WHERE r.id = ?
                ORDER BY r.name
            """
            
            cursor = conn.execute(query, (recipe_id,))
            row = cursor.fetchone()
            
            if not row:
                return None
            
            recipe_id, name, source_path, tags, servings, time_to_prepare, prepping, freezing_notes, reheat_notes, combinations, notes = row
            
            return {
                "id": recipe_id,
                "name": name,
                "source_path": source_path,
                "tags": tags,
                "servings": servings,
                "time_to_prepare": time_to_prepare,
                "prepping": prepping,
                "freezing_notes": freezing_notes,
                "reheat_notes": reheat_notes,
                "combinations": combinations,
                "notes": notes
            }
        
        finally:
            conn.close()
    
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
        recipe = self.search_recipe_by_id(recipe_id)
        
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

# Example usage:
if __name__ == "__main__":
    agent = MealAgent()
    
    # Test examples 
    result1 = agent.find_meals_with_ingredients("Find meals with chicken")
    print("Query 1 Results:", json.dumps(result1, indent=2))
    
    result2 = agent.find_meals_with_ingredients("Show me recipes with beef and onion")
    print("Query 2 Results:", json.dumps(result2, indent=2))
    
    # Test getting specific recipe
    result3 = agent.get_recipe_details(1)
    print("Recipe Detail Results:", json.dumps(result3, indent=2))