"""
Shopping Agent for Freezer Lego Meals project.

This agent generates shopping lists from one or more recipes, with features to:
- Merge duplicate ingredients
- Calculate total quantities
- Group ingredients by category
- Scale recipes appropriately
- Produce a clean shopping list

The shopping agent reuses existing database patterns from the project.
"""

import sqlite3
import json
from pathlib import Path
from typing import List, Dict, Any, Optional, Tuple
from collections import defaultdict


class ShoppingAgent:
    def __init__(self, db_path: Optional[Path] = None):
        """
        Initialize the Shopping Agent with database connection.
        
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

    def get_recipe_ingredients(self, recipe_identifier: str) -> List[Dict[str, Any]]:
        """
        Get all ingredients for a specified recipe by name or ID.
        
        Args:
            recipe_identifier: Recipe name or ID
            
        Returns:
            List of ingredient dictionaries with name, amount, unit, and other details
        """
        conn = sqlite3.connect(self.db_path)
        
        try:
            # Determine if the identifier is numeric (ID) or text (name)
            try:
                recipe_id = int(recipe_identifier)
                cursor = conn.execute("SELECT id, name FROM recipes WHERE id = ?", (recipe_identifier,))
                recipe_row = cursor.fetchone()
            except ValueError:
                # Not a number, search by name
                cursor = conn.execute("SELECT id, name FROM recipes WHERE name = ?", (recipe_identifier,))
                recipe_row = cursor.fetchone()
            
            if not recipe_row:
                return []
                
            recipe_id, recipe_name = recipe_row
            
            # Get ingredients for this recipe with serving information
            query = """
                SELECT i.name, ri.amount, ri.unit, ri.notes, r.servings
                FROM recipe_ingredients ri
                JOIN ingredients i ON ri.ingredient_id = i.id
                JOIN recipes r ON ri.recipe_id = r.id
                WHERE ri.recipe_id = ?
                ORDER BY i.name
            """
            
            cursor = conn.execute(query, (recipe_id,))
            ingredients = []
            
            for row in cursor.fetchall():
                ingredient_name, amount, unit, notes, servings = row
                ingredients.append({
                    "name": ingredient_name,
                    "amount": amount,
                    "unit": unit,
                    "notes": notes,
                    "original_recipe": recipe_name
                })
            
            return ingredients
            
        finally:
            conn.close()

    def get_multiple_recipe_ingredients(self, recipe_identifiers: List[str]) -> Dict[str, List[Dict[str, Any]]]:
        """
        Get ingredients for multiple recipes.
        
        Args:
            recipe_identifiers: List of recipe names or IDs
            
        Returns:
            Dictionary mapping recipe names to their ingredients
        """
        all_ingredients = {}
        for identifier in recipe_identifiers:
            ingredients = self.get_recipe_ingredients(identifier)
            # Use the first recipe name from our list as key
            if ingredients:
                first_ingredient = ingredients[0]
                # Extract recipe name from first ingredient's source or from identifier
                recipe_key = identifier
                all_ingredients[recipe_key] = ingredients
        return all_ingredients

    def generate_shopping_list(self, 
                              recipe_identifiers: List[str], 
                              scale_factor: float = 1.0,
                              group_by_category: bool = True) -> Dict[str, Any]:
        """
        Generate a shopping list from one or more recipes.
        
        Args:
            recipe_identifiers: List of recipe names or IDs to include
            scale_factor: Factor to scale ingredient amounts (e.g., 2.0 for double servings)
            group_by_category: Whether to group ingredients by category
            
        Returns:
            Dictionary with shopping list data and metadata
        """
        
        # Get all ingredients from specified recipes
        recipe_ingredients = self.get_multiple_recipe_ingredients(recipe_identifiers)
        
        # If no ingredients found, return empty result
        if not recipe_ingredients:
            return {
                "recipes": recipe_identifiers,
                "total_recipes": len(recipe_identifiers),
                "ingredients": [],
                "message": f"No recipes found with identifiers: {recipe_identifiers}"
            }
            
        # Aggregate ingredients and merge duplicates
        aggregated_ingredients = defaultdict(lambda: {
            'name': '',
            'amount': 0.0,
            'unit': '',
            'original_recipes': [],
            'notes': None
        })
        
        # Process each recipe's ingredients
        for recipe_name, ingredients in recipe_ingredients.items():
            for ingredient in ingredients:
                name = ingredient['name']
                amount = ingredient['amount'] or 0.0
                unit = ingredient['unit'] or ''
                notes = ingredient['notes']
                
                # Apply scaling factor
                scaled_amount = amount * scale_factor
                
                # Merge by ingredient name (case insensitive)
                normalized_name = name.lower().strip()
                current = aggregated_ingredients[normalized_name]
                
                if not current['name']:  # First time seeing this ingredient
                    current['name'] = name  # Keep original case
                    current['unit'] = unit
                else:
                    # Ensure same units for merged ingredients (not handling unit conversion here)
                    if unit and current['unit'] != unit:
                        # If we have different units, potentially we should handle this better in a real implementation
                        pass
                
                # Aggregate amounts
                current['amount'] += scaled_amount
                current['original_recipes'].append({
                    'recipe': recipe_name,
                    'amount': amount,
                    'scaled_amount': scaled_amount
                })
                
                if notes and not current['notes']:
                    current['notes'] = notes
        
        # Convert back to list format
        ingredients_list = []
        for key, data in aggregated_ingredients.items():
            # Remove duplicate recipe entries (they may have been merged)
            unique_recipes = []
            seen_recipes = set()
            for recipe_info in data['original_recipes']:
                if recipe_info['recipe'] not in seen_recipes:
                    unique_recipes.append(recipe_info)
                    seen_recipes.add(recipe_info['recipe'])
            
            data['original_recipes'] = unique_recipes
            ingredients_list.append(data)
        
        # Create result structure  
        shopping_list = {
            "recipes": recipe_identifiers,
            "total_recipes": len(recipe_identifiers),
            "scale_factor": scale_factor,
            "ingredients": ingredients_list,
            "message": f"Generated shopping list for {len(recipe_identifiers)} recipes"
        }
        
        # If grouping is enabled, add categorized structure
        if group_by_category:
            categorized_ingredients = self._categorize_ingredients(ingredients_list)
            shopping_list["categorized"] = categorized_ingredients
        
        return shopping_list

    def _categorize_ingredients(self, ingredients: List[Dict[str, Any]]) -> Dict[str, List[Dict[str, Any]]]:
        """
        Categorize ingredients into logical groups.
        
        Args:
            ingredients: List of ingredient dictionaries
            
        Returns:
            Dictionary mapping categories to lists of ingredients
        """
        # Common categories for food items
        ingredient_categories = {
            'proteins': ['chicken', 'beef', 'pork', 'fish', 'tofu', 'lamb', 'turkey', 'duck', 'shrimp', 'egg'],
            'vegetables': ['carrot', 'broccoli', 'spinach', 'onion', 'garlic', 'potato', 'tomato', 'pepper', 
                          'cucumber', 'mushroom', 'celery', 'lettuce', 'cabbage'],
            'grains': ['rice', 'pasta', 'quinoa', 'noodles', 'oats'],
            'fruits': ['apple', 'banana', 'orange', 'strawberry', 'blueberry'],
            'dairy': ['milk', 'cheese', 'yogurt', 'butter'],
            'condiments': ['sauce', 'soy sauce', 'vinegar', 'oil', 'salt', 'pepper', 'spices'],  
            'other': []  # Default for everything else
        }
        
        categorized = defaultdict(list)
        
        for ingredient in ingredients:
            name_lower = ingredient['name'].lower()
            
            # Try to find which category this ingredient belongs to
            found_category = 'other'
            for category, keywords in ingredient_categories.items():
                if any(keyword in name_lower for keyword in keywords):
                    found_category = category
                    break
            
            categorized[found_category].append(ingredient)
        
        # Convert back to regular dict for output
        return dict(categorized)

    def get_recipe_info(self, recipe_identifier: str) -> Optional[Dict[str, Any]]:
        """
        Get basic information about a specific recipe.
        
        Args:
            recipe_identifier: Recipe name or ID
            
        Returns:
            Recipe information dictionary or None if not found
        """
        conn = sqlite3.connect(self.db_path)
        
        try:
            # Determine if the identifier is numeric (ID) or text (name)
            try:
                recipe_id = int(recipe_identifier)
                cursor = conn.execute("SELECT id, name, servings, time_to_prepare FROM recipes WHERE id = ?", (recipe_identifier,))
                recipe_row = cursor.fetchone()
            except ValueError:
                # Not a number, search by name
                cursor = conn.execute("SELECT id, name, servings, time_to_prepare FROM recipes WHERE name = ?", (recipe_identifier,))
                recipe_row = cursor.fetchone()
            
            if not recipe_row:
                return None
                
            recipe_id, name, servings, time_to_prepare = recipe_row
            
            return {
                "id": recipe_id,
                "name": name,
                "servings": servings,
                "time_to_prepare": time_to_prepare
            }
            
        finally:
            conn.close()


# Example usage:
if __name__ == "__main__":
    agent = ShoppingAgent()
    
    # Test generating a shopping list with one recipe
    result1 = agent.generate_shopping_list(["Chicken Curry"])
    print("Single Recipe Results:", json.dumps(result1, indent=2))
    
    # Test with multiple recipes
    result2 = agent.generate_shopping_list(["Chicken Curry", "Beef Stir Fry"])
    print("Multiple Recipes Results:", json.dumps(result2, indent=2))
    
    # Test with scaling
    result3 = agent.generate_shopping_list(["Chicken Curry"], scale_factor=2.0)
    print("Scaled Recipe Results:", json.dumps(result3, indent=2))