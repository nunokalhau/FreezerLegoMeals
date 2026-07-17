#!/usr/bin/env python3
"""
Test script for MealService and ShoppingService implementations.
"""

import sys
import os

# Add the src directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'src'))

from services.Python.MealService import MealService
from services.Python.ShoppingService import ShoppingService

def test_meal_service():
    """Test MealService functionality."""
    print("Testing MealService...")
    
    try:
        # Initialize service
        meal_service = MealService()
        
        # Test search function
        result = meal_service.find_meals_with_ingredients("Find meals with chicken")
        print(f"Meal search completed. Found {result.get('total_recipes_found', 0)} recipes.")
        
        # Test recipe details retrieval (if a valid recipe ID exists)
        try:
            detail_result = meal_service.get_recipe_details(1)
            if 'error' not in detail_result:
                print(f"Recipe details retrieved: {detail_result['recipe']['name']}")
            else:
                print("No recipe found with ID 1")
        except Exception as e:
            print(f"Could not test recipe details: {e}")
            
    except Exception as e:
        print(f"Error in MealService test: {e}")

def test_shopping_service():
    """Test ShoppingService functionality."""
    print("\nTesting ShoppingService...")
    
    try:
        # Initialize service
        shopping_service = ShoppingService() 
        
        # Test with a simple recipe identifier (this will depend on existing data)
        result = shopping_service.generate_shopping_list(["Chicken Curry"], scale_factor=1.0)
        print(f"Shopping list generated for Chicken Curry.")
        print(f"Found {len(result.get('ingredients', []))} ingredients in the shopping list")
        
    except Exception as e:
        print(f"Error in ShoppingService test: {e}")

if __name__ == "__main__":
    print("Running service tests...")
    test_meal_service()
    test_shopping_service()
    print("\nTests completed.")