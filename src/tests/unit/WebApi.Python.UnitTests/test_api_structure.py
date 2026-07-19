#!/usr/bin/env python3

from pathlib import Path
import importlib.util

SRC_ROOT = Path(__file__).resolve().parents[3]
APP_PATH = SRC_ROOT / 'api' / 'WebApi.Python' / 'app.py'

app_spec = importlib.util.spec_from_file_location('webapi_python_app_structure', APP_PATH)
if app_spec is None or app_spec.loader is None:
    raise ImportError(f'Unable to load app module from {APP_PATH}')

app_module = importlib.util.module_from_spec(app_spec)
app_spec.loader.exec_module(app_module)


def test_root_handler_returns_success_payload():
    payload = app_module.read_root()

    assert payload['status'] == 'success'
    assert 'Welcome' in payload['message']


def test_get_recipe_by_id_returns_wrapped_recipe(monkeypatch):
    monkeypatch.setattr(app_module.meal_service, 'get_recipe_by_id', lambda _id: {'id': 1, 'name': 'Chicken Rice'})

    response = app_module.get_recipe_by_id(1)

    assert response.recipe['id'] == 1
    assert response.recipe['name'] == 'Chicken Rice'


def test_get_recipe_details_returns_wrapped_details(monkeypatch):
    monkeypatch.setattr(
        app_module.meal_service,
        'get_recipe_details',
        lambda _id: {'recipe': {'id': 1, 'name': 'Chicken Rice'}, 'message': 'Details for recipe: Chicken Rice'}
    )

    response = app_module.get_recipe_details(1)

    assert response.recipe['name'] == 'Chicken Rice'
    assert 'Details for recipe' in response.message


def test_get_recipe_ingredients_returns_found_response(monkeypatch):
    monkeypatch.setattr(
        app_module.shopping_service,
        'get_recipe_ingredients',
        lambda _identifier: [{'name': 'chicken', 'amount': 200, 'unit': 'g'}]
    )

    response = app_module.get_recipe_ingredients('Chicken Rice')

    assert response.found is True
    assert response.ingredients[0]['name'] == 'chicken'


def test_get_multiple_recipe_ingredients_accepts_list_body(monkeypatch):
    monkeypatch.setattr(
        app_module.shopping_service,
        'get_multiple_recipe_ingredients',
        lambda identifiers: {identifier: [{'name': 'salt'}] for identifier in identifiers}
    )

    response = app_module.get_multiple_recipe_ingredients(['Recipe A', 'Recipe B'])

    assert response.total_recipes == 2
    assert response.recipe_ingredients['Recipe A'][0]['name'] == 'salt'


def test_generate_shopping_list_returns_wrapped_payload(monkeypatch):
    monkeypatch.setattr(
        app_module.shopping_service,
        'generate_shopping_list',
        lambda identifiers, scale_factor, group_by_category: {
            'recipes': identifiers,
            'total_recipes': len(identifiers),
            'scale_factor': scale_factor,
            'ingredients': [{'name': 'chicken', 'amount': 200, 'unit': 'g'}],
            'message': 'Generated shopping list'
        }
    )

    response = app_module.generate_shopping_list(
        app_module.GenerateShoppingListRequest(
            recipe_identifiers=['Chicken Rice'],
            scale_factor=2.0,
            group_by_category=True,
        )
    )

    assert response.scale_factor == 2.0
    assert response.shopping_list['total_recipes'] == 1


def test_get_recipe_info_returns_wrapped_info(monkeypatch):
    monkeypatch.setattr(
        app_module.shopping_service,
        'get_recipe_info',
        lambda _identifier: {'id': 1, 'name': 'Chicken Rice', 'servings': 2, 'time_to_prepare': 25}
    )

    response = app_module.get_recipe_info('Chicken Rice')

    assert response.info['servings'] == 2
    assert response.info['time_to_prepare'] == 25