#!/usr/bin/env python3
"""Integration tests for Python recipes and shopping endpoints."""

import asyncio
import importlib.util
from pathlib import Path

import httpx
import pytest


SRC_ROOT = Path(__file__).resolve().parents[3]
APP_PATH = SRC_ROOT / "api" / "WebApi.Python" / "app.py"


def load_app_module():
    app_spec = importlib.util.spec_from_file_location("webapi_python_app_recipes_shopping_integration", APP_PATH)
    if app_spec is None or app_spec.loader is None:
        raise ImportError(f"Unable to load app module from {APP_PATH}")

    app_module = importlib.util.module_from_spec(app_spec)
    app_spec.loader.exec_module(app_module)
    return app_module


def test_recipes_search_and_details_end_to_end():
    app_module = load_app_module()

    search_response = asyncio.run(post_json(app_module.app, "/api/recipes/search", {"ingredients": ["chicken", "rice"]}))
    assert search_response.status_code == 200
    search_payload = search_response.json()
    assert isinstance(search_payload.get("recipes"), list)
    assert isinstance(search_payload.get("total_recipes_found"), int)

    recipe_id = asyncio.run(find_existing_recipe_id(app_module.app))
    if recipe_id is None:
        pytest.skip("No recipes available for end-to-end recipe detail checks.")

    by_id_response = asyncio.run(get(app_module.app, f"/api/recipes/{recipe_id}"))
    assert by_id_response.status_code == 200
    by_id_payload = by_id_response.json()
    assert by_id_payload.get("recipe")
    assert by_id_payload["recipe"]["id"] == recipe_id

    details_response = asyncio.run(get(app_module.app, f"/api/recipes/{recipe_id}/details"))
    assert details_response.status_code == 200
    details_payload = details_response.json()
    assert details_payload.get("recipe")


def test_recipes_endpoints_validation_and_not_found_paths():
    app_module = load_app_module()

    empty_search = asyncio.run(post_json(app_module.app, "/api/recipes/search", {"ingredients": []}))
    assert empty_search.status_code == 400

    invalid_id = asyncio.run(get(app_module.app, "/api/recipes/0"))
    assert invalid_id.status_code == 400

    missing_id = asyncio.run(get(app_module.app, "/api/recipes/999999"))
    assert missing_id.status_code == 404

    invalid_details_id = asyncio.run(get(app_module.app, "/api/recipes/0/details"))
    assert invalid_details_id.status_code == 400

    missing_details = asyncio.run(get(app_module.app, "/api/recipes/999999/details"))
    assert missing_details.status_code == 404

    empty_find = asyncio.run(post_json(app_module.app, "/api/recipes/find-by-ingredients", {"query": " "}))
    assert empty_find.status_code == 400


def test_shopping_endpoints_end_to_end_and_validation():
    app_module = load_app_module()
    recipe_id = asyncio.run(find_existing_recipe_id(app_module.app))
    if recipe_id is None:
        pytest.skip("No recipes available for shopping end-to-end checks.")

    recipe_identifier = str(recipe_id)

    recipe_info = asyncio.run(get(app_module.app, f"/api/shopping/{recipe_identifier}/info"))
    assert recipe_info.status_code == 200
    assert recipe_info.json().get("info")

    ingredients = asyncio.run(get(app_module.app, f"/api/shopping/ingredients/{recipe_identifier}"))
    assert ingredients.status_code == 200
    ingredients_payload = ingredients.json()
    assert isinstance(ingredients_payload.get("ingredients"), list)

    multi_ingredients = asyncio.run(post_json(app_module.app, "/api/shopping/ingredients", [recipe_identifier]))
    assert multi_ingredients.status_code == 200
    multi_payload = multi_ingredients.json()
    assert isinstance(multi_payload.get("recipe_ingredients"), dict)

    shopping_list = asyncio.run(post_json(app_module.app, "/api/shopping/generate", {
        "recipe_identifiers": [recipe_identifier],
        "scale_factor": 1.0,
        "group_by_category": True,
    }))
    assert shopping_list.status_code == 200
    shopping_payload = shopping_list.json()
    assert shopping_payload.get("shopping_list")

    bad_multi = asyncio.run(post_json(app_module.app, "/api/shopping/ingredients", []))
    assert bad_multi.status_code == 400

    bad_generate = asyncio.run(post_json(app_module.app, "/api/shopping/generate", {
        "recipe_identifiers": [],
        "scale_factor": 1.0,
        "group_by_category": True,
    }))
    assert bad_generate.status_code == 400

    bad_scale = asyncio.run(post_json(app_module.app, "/api/shopping/generate", {
        "recipe_identifiers": [recipe_identifier],
        "scale_factor": 0,
        "group_by_category": True,
    }))
    assert bad_scale.status_code == 400

    missing_info = asyncio.run(get(app_module.app, "/api/shopping/999999/info"))
    assert missing_info.status_code == 404


async def post_json(app, path: str, payload):
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://testserver") as client:
        return await client.post(path, json=payload)


async def get(app, path: str):
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://testserver") as client:
        return await client.get(path)


async def find_existing_recipe_id(app, max_id: int = 200):
    for candidate_id in range(1, max_id + 1):
        response = await get(app, f"/api/recipes/{candidate_id}")
        if response.status_code == 200:
            payload = response.json()
            recipe = payload.get("recipe") or {}
            recipe_id = recipe.get("id")
            if isinstance(recipe_id, int) and recipe_id > 0:
                return recipe_id

    return None
