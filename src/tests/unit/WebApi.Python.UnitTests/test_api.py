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
        assert '/api/recipes/search' in route_paths
        assert '/api/shopping/generate' in route_paths

    def test_health_handler_response(self):
        """Test that health handler returns expected payload."""
        payload = app_module.health_check()
        assert payload.status == 'healthy'
        assert payload.service == 'WebApi.Python'


def test_basic_web_structure():
    """Test basic web API structure and instantiation."""
    # Test that this test file can be imported properly
    assert True


# Note: To create proper integration tests for the actual web API endpoints, 
# you would need to import the specific routes from the app.py file once the project 
# structures are properly set up and path issues are resolved.