#!/usr/bin/env python3
"""
Unit tests for MealService and ShoppingService implementations.
These are proper pytest-based unit tests with mocked repository dependencies.
"""

import sys
import os
import unittest.mock as mock
from pathlib import Path

test_dir = os.path.dirname(os.path.abspath(__file__))
src_dir = os.path.join(test_dir, '..', '..', 'src')
sys.path.insert(0, os.path.abspath(src_dir))

import pytest
from services.Services.Python.meal_service import MealService
from services.Services.Python.shopping_service import ShoppingService


class TestMealService:
    """Unit tests for MealService."""
    
    def test_initialization(self):
        """Test that MealService can be initialized successfully."""
        service = MealService()
        assert service is not None
        assert hasattr(service, 'repository')
    
    @mock.patch('services.Services.Python.meal_service.Repository')
    def test_search_recipes_by_ingredients(self, mock_repo_class):
        """Test search_recipes_by_ingredients method."""
        mock_repo_instance = mock.Mock()
        mock_repo_class.return_value = mock_repo_instance
        
        expected_data = [
            {"id": 1, "name": "Chicken Curry", "source_path": "/recipes/chicken_curry.md", "matched_ingredients": ["chicken"]}
        ]
        mock_repo_instance.search_recipes_by_ingredients.return_value = expected_data
        
        service = MealService()
        result = service.search_recipes_by_ingredients(["chicken"])
        
        assert result == expected_data
        mock_repo_instance.search_recipes_by_ingredients.assert_called_once_with(["chicken"])
    
    @mock.patch('services.Services.Python.meal_service.Repository')
    def test_get_recipe_by_id(self, mock_repo_class):
        """Test get_recipe_by_id method."""
        mock_repo_instance = mock.Mock()
        mock_repo_class.return_value = mock_repo_instance
        
        expected_data = {
            "id": 1,
            "name": "Chicken Curry",
            "source_path": "/recipes/chicken_curry.md"
        }
        mock_repo_instance.get_recipe_by_id.return_value = expected_data
        
        service = MealService()
        result = service.get_recipe_by_id(1)
        
        assert result == expected_data
        mock_repo_instance.get_recipe_by_id.assert_called_once_with(1)
    
    @mock.patch('services.Services.Python.meal_service.Repository')
    def test_find_meals_with_ingredients_success(self, mock_repo_class):
        """Test find_meals_with_ingredients with ingredients found."""
        mock_repo_instance = mock.Mock()
        mock_repo_class.return_value = mock_repo_instance
        
        expected_data = [
            {"id": 1, "name": "Chicken Curry", "source_path": "/recipes/chicken_curry.md", "matched_ingredients": ["chicken"]}
        ]
        mock_repo_instance.search_recipes_by_ingredients.return_value = expected_data
        
        service = MealService()
        result = service.find_meals_with_ingredients("Find meals with chicken")
        
        assert "query" in result
        assert "search_terms" in result
        assert "total_recipes_found" in result
        assert result["total_recipes_found"] == 1
        
    @mock.patch('services.Services.Python.meal_service.Repository')
    def test_find_meals_with_ingredients_no_ingredients(self, mock_repo_class):
        """Test find_meals_with_ingredients when no ingredients found."""
        mock_repo_instance = mock.Mock()
        mock_repo_class.return_value = mock_repo_instance
        
        mock_repo_instance.search_recipes_by_ingredients.return_value = []
        
        service = MealService()
        result = service.find_meals_with_ingredients("Find meals with nonexistent ingredient")
        
        assert result["total_recipes_found"] == 0
        assert "message" in result
    
    @mock.patch('services.Services.Python.meal_service.Repository')
    def test_get_recipe_details_success(self, mock_repo_class):
        """Test get_recipe_details with valid recipe."""
        mock_repo_instance = mock.Mock()
        mock_repo_class.return_value = mock_repo_instance
        
        expected_data = {
            "id": 1,
            "name": "Chicken Curry",
            "source_path": "/recipes/chicken_curry.md"
        }
        mock_repo_instance.get_recipe_by_id.return_value = expected_data
        
        service = MealService()
        result = service.get_recipe_details(1)
        
        assert result["recipe"] == expected_data
        assert "message" in result

    @mock.patch('services.Services.Python.meal_service.Repository')
    def test_get_recipe_details_not_found(self, mock_repo_class):
        """Test get_recipe_details with invalid recipe."""
        mock_repo_instance = mock.Mock()
        mock_repo_class.return_value = mock_repo_instance
        
        mock_repo_instance.get_recipe_by_id.return_value = None
        
        service = MealService()
        result = service.get_recipe_details(999)
        
        assert "error" in result


class TestShoppingService:
    """Unit tests for ShoppingService."""
    
    def test_initialization(self):
        """Test that ShoppingService can be initialized successfully."""
        service = ShoppingService()
        assert service is not None
        assert hasattr(service, 'repository')
    
    @mock.patch('services.Services.Python.shopping_service.Repository')
    def test_get_recipe_ingredients_success(self, mock_repo_class):
        """Test get_recipe_ingredients method with valid recipe."""
        mock_repo_instance = mock.Mock()
        mock_repo_class.return_value = mock_repo_instance
        
        expected_data = [
            {"name": "chicken breast", "amount": 500.0, "unit": "g"}
        ]
        mock_repo_instance.get_recipe_ingredients.return_value = expected_data
        
        service = ShoppingService()
        result = service.get_recipe_ingredients("Chicken Curry")
        
        assert result == expected_data
        mock_repo_instance.get_recipe_ingredients.assert_called_once_with(1)
    
    @mock.patch('services.Services.Python.shopping_service.Repository')
    def test_get_multiple_recipe_ingredients(self, mock_repo_class):
        """Test get_multiple_recipe_ingredients method."""
        mock_repo_instance = mock.Mock()
        mock_repo_class.return_value = mock_repo_instance
        
        expected_data = {
            "Chicken Curry": [{"name": "chicken breast", "amount": 500.0, "unit": "g"}]
        }
        mock_repo_instance.get_recipe_ingredients.return_value = expected_data["Chicken Curry"]
        
        service = ShoppingService()
        result = service.get_multiple_recipe_ingredients(["Chicken Curry"])
        
        assert "Chicken Curry" in result
        assert len(result["Chicken Curry"]) == 1
    
    @mock.patch('services.Services.Python.shopping_service.Repository')
    def test_generate_shopping_list_basic(self, mock_repo_class):
        """Test generate_shopping_list with basic functionality."""
        mock_repo_instance = mock.Mock()
        mock_repo_class.return_value = mock_repo_instance
        
        mock_repo_instance.get_recipe_ingredients.side_effect = [
            [{"name": "chicken breast", "amount": 500.0, "unit": "g"}],
            [{"name": "onion", "amount": 2.0, "unit": "pcs"}]
        ]
        
        service = ShoppingService()
        result = service.generate_shopping_list(["Chicken Curry", "Vegetable Soup"], scale_factor=1.0)
        
        assert "recipes" in result
        assert "total_recipes" in result
        assert "ingredients" in result
        assert result["total_recipes"] == 2
    
    @mock.patch('services.Services.Python.shopping_service.Repository')
    def test_generate_shopping_list_empty(self, mock_repo_class):
        """Test generate_shopping_list with empty recipe list."""
        mock_repo_instance = mock.Mock()
        mock_repo_class.return_value = mock_repo_instance
        
        service = ShoppingService()
        result = service.generate_shopping_list([], scale_factor=1.0)
        
        assert "recipes" in result
        assert result["total_recipes"] == 0
        assert "ingredients" in result
    
    @mock.patch('services.Services.Python.shopping_service.Repository')
    def test_generate_shopping_list_with_scaling(self, mock_repo_class):
        """Test generate_shopping_list with scale factor."""
        mock_repo_instance = mock.Mock()
        mock_repo_class.return_value = mock_repo_instance
        
        mock_repo_instance.get_recipe_ingredients.return_value = [
            {"name": "chicken breast", "amount": 500.0, "unit": "g"}
        ]
        
        service = ShoppingService()
        result = service.generate_shopping_list(["Chicken Curry"], scale_factor=2.0)
        
        assert result["scale_factor"] == 2.0
        
    @mock.patch('services.Services.Python.shopping_service.Repository')
    def test_categorize_ingredients(self, mock_repo_class):
        """Test _categorize_ingredients helper method."""
        mock_repo_instance = mock.Mock()
        mock_repo_class.return_value = mock_repo_instance
        
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