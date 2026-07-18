"""
Services layer for Freezer Lego Meals.
This layer provides business logic and service classes that coordinate 
with the repository layer for data access.
"""

# Import all services here to make them available at package level
from .meal_service import MealService
from .shopping_service import ShoppingService

__all__ = [
    'MealService',
    'ShoppingService'
]