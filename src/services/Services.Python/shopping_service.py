"""
Shopping Service for Freezer Lego Meals.
This service layer handles business logic for shopping list generation and management.
"""

from pathlib import Path
from typing import List, Dict, Any, Optional
from collections import defaultdict
from dataclasses import dataclass

# Import Repository from the repository module
import importlib.util
spec = importlib.util.spec_from_file_location("Repository", "src/repository/Repository.Python")
Repository = importlib.util.module_from_spec(spec)
spec.loader.exec_module(Repository)

@dataclass
class Ingredient:
    """Data class representing an ingredient."""
    name: str
    amount: float
    unit: str
    notes: Optional[str] = None
    original_recipe: str = ""


@dataclass
class ShoppingListResult:
    """Data class representing a shopping list result."""
    recipes: List[str]
    total_recipes: int
    scale_factor: float
    ingredients: List[Dict[str, Any]]
    categorized: Optional[Dict[str, List[Dict[str, Any]]]] = None
    message: str = ""


class ShoppingService:
    """Provides business logic for shopping list generation and management."""
    
    def __init__(self, db_path: Optional[Path] = None):
        """
        Initialize the Shopping Service with database connection.
        
        Args:
            db_path: Path to SQLite database. If None, uses default project location.
        """
        self.repository = Repository(db_path)
    
    def get_recipe_ingredients(self, recipe_identifier: str) -> List[Dict[str, Any]]:
        """
        Get all ingredients for a specified recipe by name or ID.
        
        Args:
            recipe_identifier: Recipe name or ID
            
        Returns:
            List of ingredient dictionaries with name, amount, unit, and other details
        """
        # First get the recipe_id from either name or id
        try:
            recipe_id = int(recipe_identifier)
        except ValueError:
            # Not a number, so search by name
            recipes = self.repository.search_recipes_by_ingredients([recipe_identifier])
            if not recipes:
                return []
            recipe_id = recipes[0]["id"]
            
        # Get ingredients using the repository
        return self.repository.get_recipe_ingredients(recipe_id)
    
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
        # First get the recipe_id from either name or id
        try:
            recipe_id = int(recipe_identifier)
        except ValueError:
            # Not a number, so search by name  
            recipes = self.repository.search_recipes_by_ingredients([recipe_identifier])
            if not recipes:
                return None
            recipe_id = recipes[0]["id"]
            
        # Return recipe info
        recipe_details = self.repository.get_recipe_details(recipe_identifier) 
        if recipe_details:
            # Extract the first one which should contain the basic info
            return {
                "id": recipe_details[0].get("id"),
                "name": recipe_details[0].get("name"),
                "servings": recipe_details[0].get("servings"),
                "time_to_prepare": recipe_details[0].get("time_to_prepare")
            }
        return None