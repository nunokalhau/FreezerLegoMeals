#!/usr/bin/env python3
"""
Unit tests for MealService and ShoppingService implementations.
These are proper pytest-based unit tests with mocked repository dependencies.
"""

import sys
import os
import unittest.mock as mock
from pathlib import Path
import importlib.util

SRC_ROOT = Path(__file__).resolve().parents[3]
MEAL_SERVICE_PATH = SRC_ROOT / 'services' / 'Services.Python' / 'meal_service.py'
SHOPPING_SERVICE_PATH = SRC_ROOT / 'services' / 'Services.Python' / 'shopping_service.py'

meal_spec = importlib.util.spec_from_file_location('meal_service', MEAL_SERVICE_PATH)
shopping_spec = importlib.util.spec_from_file_location('shopping_service', SHOPPING_SERVICE_PATH)

if meal_spec is None or meal_spec.loader is None:
    raise ImportError(f'Unable to load meal_service from {MEAL_SERVICE_PATH}')
if shopping_spec is None or shopping_spec.loader is None:
    raise ImportError(f'Unable to load shopping_service from {SHOPPING_SERVICE_PATH}')

meal_module = importlib.util.module_from_spec(meal_spec)
meal_spec.loader.exec_module(meal_module)
shopping_module = importlib.util.module_from_spec(shopping_spec)
shopping_spec.loader.exec_module(shopping_module)

# Register modules so unittest.mock.patch can resolve by module name.
sys.modules['meal_service'] = meal_module
sys.modules['shopping_service'] = shopping_module

import pytest
MealService = meal_module.MealService
ShoppingService = shopping_module.ShoppingService


class TestMealService:
    """Unit tests for MealService."""
    
    def test_initialization(self):
        """Test that MealService can be initialized successfully."""
        service = MealService()
        assert service is not None
        assert hasattr(service, 'repository')
    
    def test_search_recipes_by_ingredients(self):
        """Test search_recipes_by_ingredients method."""
        mock_repo_instance = mock.Mock()
        
        expected_data = [
            {"id": 1, "name": "Chicken Curry", "source_path": "/recipes/chicken_curry.md", "matched_ingredients": ["chicken"]}
        ]
        mock_repo_instance.search_recipes_by_ingredients.return_value = expected_data
        
        service = MealService()
        service.repository = mock_repo_instance
        result = service.search_recipes_by_ingredients(["chicken"])
        
        assert result == expected_data
        mock_repo_instance.search_recipes_by_ingredients.assert_called_once_with(["chicken"])
    
    def test_get_recipe_by_id(self):
        """Test get_recipe_by_id method."""
        mock_repo_instance = mock.Mock()
        
        expected_data = {
            "id": 1,
            "name": "Chicken Curry",
            "source_path": "/recipes/chicken_curry.md"
        }
        mock_repo_instance.get_recipe_by_id.return_value = expected_data
        
        service = MealService()
        service.repository = mock_repo_instance
        result = service.get_recipe_by_id(1)
        
        assert result == expected_data
        mock_repo_instance.get_recipe_by_id.assert_called_once_with(1)
    
    def test_find_meals_with_ingredients_success(self):
        """Test find_meals_with_ingredients with ingredients found."""
        mock_repo_instance = mock.Mock()
        
        expected_data = [
            {"id": 1, "name": "Chicken Curry", "source_path": "/recipes/chicken_curry.md", "matched_ingredients": ["chicken"]}
        ]
        mock_repo_instance.search_recipes_by_ingredients.return_value = expected_data
        
        service = MealService()
        service.repository = mock_repo_instance
        result = service.find_meals_with_ingredients("Find meals with chicken")
        
        assert "query" in result
        assert "search_terms" in result
        assert "total_recipes_found" in result
        assert result["total_recipes_found"] == 1
        
    def test_find_meals_with_ingredients_no_ingredients(self):
        """Test find_meals_with_ingredients when no ingredients found."""
        mock_repo_instance = mock.Mock()
        
        mock_repo_instance.search_recipes_by_ingredients.return_value = []
        
        service = MealService()
        service.repository = mock_repo_instance
        result = service.find_meals_with_ingredients("Find meals with nonexistent ingredient")
        
        assert result["total_recipes_found"] == 0
        assert "message" in result
    
    def test_get_recipe_details_success(self):
        """Test get_recipe_details with valid recipe."""
        mock_repo_instance = mock.Mock()
        
        expected_data = {
            "id": 1,
            "name": "Chicken Curry",
            "source_path": "/recipes/chicken_curry.md"
        }
        mock_repo_instance.get_recipe_by_id.return_value = expected_data
        
        service = MealService()
        service.repository = mock_repo_instance
        result = service.get_recipe_details(1)
        
        assert result["recipe"] == expected_data
        assert "message" in result

    def test_get_recipe_details_not_found(self):
        """Test get_recipe_details with invalid recipe."""
        mock_repo_instance = mock.Mock()
        
        mock_repo_instance.get_recipe_by_id.return_value = None
        
        service = MealService()
        service.repository = mock_repo_instance
        result = service.get_recipe_details(999)
        
        assert "error" in result


class TestShoppingService:
    """Unit tests for ShoppingService."""
    
    def test_initialization(self):
        """Test that ShoppingService can be initialized successfully."""
        service = ShoppingService()
        assert service is not None
        assert hasattr(service, 'repository')
    
    def test_get_recipe_ingredients_success(self):
        """Test get_recipe_ingredients method with valid recipe."""
        mock_repo_instance = mock.Mock()
        
        expected_data = [
            {"name": "chicken breast", "amount": 500.0, "unit": "g"}
        ]
        mock_repo_instance.get_recipe_ingredients.return_value = expected_data
        mock_repo_instance.get_recipe_details.return_value = [
            {"id": 1, "name": "Chicken Curry"}
        ]
        
        service = ShoppingService()
        service.repository = mock_repo_instance
        result = service.get_recipe_ingredients("Chicken Curry")
        
        assert result == expected_data
        mock_repo_instance.get_recipe_ingredients.assert_called_once_with(1)
    
    def test_get_multiple_recipe_ingredients(self):
        """Test get_multiple_recipe_ingredients method."""
        mock_repo_instance = mock.Mock()
        
        expected_data = {
            "Chicken Curry": [{"name": "chicken breast", "amount": 500.0, "unit": "g"}]
        }
        mock_repo_instance.get_recipe_ingredients.return_value = expected_data["Chicken Curry"]
        mock_repo_instance.get_recipe_details.return_value = [
            {"id": 1, "name": "Chicken Curry"}
        ]
        
        service = ShoppingService()
        service.repository = mock_repo_instance
        result = service.get_multiple_recipe_ingredients(["Chicken Curry"])
        
        assert "Chicken Curry" in result
        assert len(result["Chicken Curry"]) == 1
    
    def test_generate_shopping_list_basic(self):
        """Test generate_shopping_list with basic functionality."""
        mock_repo_instance = mock.Mock()
        
        mock_repo_instance.get_recipe_ingredients.side_effect = [
            [{"name": "chicken breast", "amount": 500.0, "unit": "g"}],
            [{"name": "onion", "amount": 2.0, "unit": "pcs"}]
        ]
        mock_repo_instance.get_recipe_details.side_effect = [
            [{"id": 1, "name": "Chicken Curry"}],
            [{"id": 2, "name": "Vegetable Soup"}],
        ]
        
        service = ShoppingService()
        service.repository = mock_repo_instance
        result = service.generate_shopping_list(["Chicken Curry", "Vegetable Soup"], scale_factor=1.0)
        
        assert "recipes" in result
        assert "total_recipes" in result
        assert "ingredients" in result
        assert result["total_recipes"] == 2
    
    def test_generate_shopping_list_empty(self):
        """Test generate_shopping_list with empty recipe list."""
        mock_repo_instance = mock.Mock()
        
        service = ShoppingService()
        service.repository = mock_repo_instance
        result = service.generate_shopping_list([], scale_factor=1.0)
        
        assert "recipes" in result
        assert result["total_recipes"] == 0
        assert "ingredients" in result
    
    def test_generate_shopping_list_with_scaling(self):
        """Test generate_shopping_list with scale factor."""
        mock_repo_instance = mock.Mock()
        
        mock_repo_instance.get_recipe_ingredients.return_value = [
            {"name": "chicken breast", "amount": 500.0, "unit": "g"}
        ]
        mock_repo_instance.get_recipe_details.return_value = [
            {"id": 1, "name": "Chicken Curry"}
        ]
        
        service = ShoppingService()
        service.repository = mock_repo_instance
        result = service.generate_shopping_list(["Chicken Curry"], scale_factor=2.0)
        
        assert result["scale_factor"] == 2.0

    def test_get_recipe_info_success(self):
        """Test get_recipe_info returns mapped recipe info."""
        mock_repo_instance = mock.Mock()
        mock_repo_instance.get_recipe_details.return_value = [
            {
                "id": 1,
                "name": "Chicken Curry",
                "servings": 2,
                "time_to_prepare": 30,
            }
        ]

        service = ShoppingService()
        service.repository = mock_repo_instance

        info = service.get_recipe_info("Chicken Curry")

        assert info is not None
        assert info["id"] == 1
        assert info["name"] == "Chicken Curry"

    def test_get_recipe_info_not_found(self):
        """Test get_recipe_info returns None when recipe is not found."""
        mock_repo_instance = mock.Mock()
        mock_repo_instance.get_recipe_details.return_value = []

        service = ShoppingService()
        service.repository = mock_repo_instance

        info = service.get_recipe_info("Missing Recipe")

        assert info is None

    def test_get_recipe_ingredients_returns_empty_for_blank_identifier(self):
        """Test get_recipe_ingredients returns empty for blank identifiers."""
        service = ShoppingService()
        service.repository = mock.Mock()

        assert service.get_recipe_ingredients("   ") == []

    def test_get_recipe_info_returns_none_for_blank_identifier(self):
        """Test get_recipe_info returns None for blank identifiers."""
        service = ShoppingService()
        service.repository = mock.Mock()

        assert service.get_recipe_info("   ") is None
        
    def test_categorize_ingredients(self):
        """Test _categorize_ingredients helper method."""
        service = ShoppingService()
        
        ingredients = [
            {"name": "chicken breast", "amount": 500.0, "unit": "g"},
            {"name": "onion", "amount": 2.0, "unit": "pcs"}
        ]
        
        categorized = service._categorize_ingredients(ingredients)
        
        assert isinstance(categorized, dict)
        assert len(categorized) >= 1


def test_service_direct_instantiation():
    """Test that services can be instantiated without errors (smoke test)."""
    meal_service = MealService()
    shopping_service = ShoppingService()
    
    assert meal_service is not None
    assert shopping_service is not None


if __name__ == "__main__":
    pytest.main([__file__, "-v"])