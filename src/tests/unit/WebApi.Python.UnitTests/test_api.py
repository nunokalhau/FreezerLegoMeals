#!/usr/bin/env python3
"""
Unit tests for the Freezer Lego Meals Python Web API.
These are proper pytest-based unit tests for API endpoints and functionality.
"""

import sys
import os
from pathlib import Path
import importlib.util

SRC_ROOT = Path(__file__).resolve().parents[3]
APP_PATH = SRC_ROOT / 'api' / 'WebApi.Python' / 'app.py'

import pytest
from fastapi import HTTPException

app_spec = importlib.util.spec_from_file_location('webapi_python_app', APP_PATH)
if app_spec is None or app_spec.loader is None:
    raise ImportError(f'Unable to load app module from {APP_PATH}')

app_module = importlib.util.module_from_spec(app_spec)
app_spec.loader.exec_module(app_module)
app = app_module.app


class TestWebAPI:
    """Unit tests for Web API endpoints."""
    
    def test_api_initialization(self):
        """Test that API can be initialized successfully."""
        assert app is not None
        assert app.title == 'Freezer Lego Meals Python API'
    
    def test_app_routes_exist(self):
        """Test that key API routes are defined."""
        route_paths = {route.path for route in app.routes}
        assert '/health' in route_paths
        assert '/api/assistant/chat' in route_paths
        assert '/api/recipes/search' in route_paths
        assert '/api/shopping/generate' in route_paths

    def test_health_handler_response(self):
        """Test that health handler returns expected payload."""
        payload = app_module.health_check()
        assert payload.status == 'healthy'
        assert payload.service == 'WebApi.Python'

    def test_search_recipes_rejects_empty_ingredients(self):
        with pytest.raises(HTTPException) as exc:
            app_module.search_recipes(app_module.RecipeSearchRequest(ingredients=[]))

        assert exc.value.status_code == 400

    def test_search_recipes_returns_wrapped_payload(self, monkeypatch):
        monkeypatch.setattr(
            app_module.meal_service,
            'search_recipes_by_ingredients',
            lambda _ingredients: [{"id": 1, "name": "Chicken Rice"}]
        )

        response = app_module.search_recipes(app_module.RecipeSearchRequest(ingredients=['chicken']))

        assert response.total_recipes_found == 1
        assert response.recipes[0]['name'] == 'Chicken Rice'

    def test_chat_with_assistant_returns_wrapped_payload(self, monkeypatch):
        monkeypatch.setattr(app_module.assistant_service, 'chat', lambda message: f'assistant:{message}')

        response = app_module.chat_with_assistant(
            app_module.AssistantChatRequest(message='Hello')
        )

        assert response.response == 'assistant:Hello'

    def test_chat_with_assistant_rejects_empty_message(self):
        with pytest.raises(HTTPException) as exc:
            app_module.chat_with_assistant(app_module.AssistantChatRequest(message=' '))

        assert exc.value.status_code == 400

    def test_generate_shopping_list_rejects_invalid_scale_factor(self):
        with pytest.raises(HTTPException) as exc:
            app_module.generate_shopping_list(
                app_module.GenerateShoppingListRequest(
                    recipe_identifiers=['Chicken Rice'],
                    scale_factor=0,
                    group_by_category=True,
                )
            )

        assert exc.value.status_code == 400

    def test_get_recipe_info_returns_error_response_when_missing(self, monkeypatch):
        monkeypatch.setattr(app_module.shopping_service, 'get_recipe_info', lambda _identifier: None)

        with pytest.raises(HTTPException) as exc:
            app_module.get_recipe_info('missing')

        assert exc.value.status_code == 404

    def test_get_recipe_by_id_returns_not_found_when_missing(self, monkeypatch):
        monkeypatch.setattr(app_module.meal_service, 'get_recipe_by_id', lambda _id: None)

        with pytest.raises(HTTPException) as exc:
            app_module.get_recipe_by_id(123)

        assert exc.value.status_code == 404

    def test_get_recipe_details_returns_not_found_when_missing(self, monkeypatch):
        monkeypatch.setattr(app_module.meal_service, 'get_recipe_details', lambda _id: {'error': 'missing'})

        with pytest.raises(HTTPException) as exc:
            app_module.get_recipe_details(123)

        assert exc.value.status_code == 404


def test_basic_web_structure():
    """Test basic web API structure and instantiation."""
    # Test that this test file can be imported properly
    assert True


# Note: To create proper integration tests for the actual web API endpoints, 
# you would need to import the specific routes from the app.py file once the project 
# structures are properly set up and path issues are resolved.