import importlib.util
from pathlib import Path

SCRIPT_PATH = Path(__file__).resolve().parents[3] / "scripts" / "ai_tools.py"
spec = importlib.util.spec_from_file_location("ai_tools_test", SCRIPT_PATH)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def test_search_recipes_returns_recipe_discovery_cards():
    result = module.search_recipes({"ingredients": ["chicken"], "freezer_friendly": True, "limit": 1})

    assert result["total_recipes_found"] == 1
    assert result["recipes"][0]["id"] == "salsa_verde_chicken"
    assert result["recipes"][0]["name"] == "Salsa Verde Chicken"
    assert result["recipes"][0]["functional_role"] == "MainCourse"
    assert result["recipes"][0]["is_standalone_meal"] is True
    assert result["recipes"][0]["freezer_friendly"] is True
    assert "high-protein" in result["recipes"][0]["tags"]


def test_plan_weekly_meals_uses_filtered_candidates():
    result = module.plan_weekly_meals({"number_of_days": 2, "meals_per_day": 1, "dietary_preferences": ["high protein"]})

    assert len(result["days"]) == 2
    first_recipe = result["days"][0]["meals"][0]["recipe"]
    assert "high-protein" in first_recipe["tags"]
    assert first_recipe["functional_role"] in {"CompleteMeal", "MainCourse", "Breakfast", "Snack", "Dessert"}


def test_plan_weekly_meals_does_not_return_sauces_as_standalone_meals():
    result = module.plan_weekly_meals({"number_of_days": 1, "meals_per_day": 3})

    meals = result["days"][0]["meals"]
    standalone_roles = {meal["recipe"]["functional_role"] for meal in meals}
    assert "Sauce" not in standalone_roles
    assert "Condiment" not in standalone_roles
    assert "SideDish" not in standalone_roles
    assert "Base" not in standalone_roles
    assert all(meal["recipe"]["is_standalone_meal"] is True for meal in meals)


def test_replace_meal_avoids_current_recipe():
    result = module.replace_meal({"current_recipe": "Turkey Chili", "meal_type": "dinner"})

    assert result["meal_type"] == "dinner"
    assert result["replacement"]["name"] != "Turkey Chili"
    assert result["replacement"]["functional_role"] in {"CompleteMeal", "MainCourse"}


def test_get_recipe_returns_full_details_for_search_card_id():
    result = module.get_recipe({"id": "salsa_verde_chicken"})

    assert result["recipe"]["id"] == "salsa_verde_chicken"
    assert result["numeric_id"] == 2
    assert result["functional_role"] == "MainCourse"
    assert any(ingredient["name"] == "frango" for ingredient in result["ingredients"])


def test_optimize_shopping_list_merges_and_groups_items():
    result = module.optimize_shopping_list({"items": [
        {"name": "Fresh Onion", "amount": 1, "unit": "unit"},
        {"name": "onion", "amount": 2, "unit": "unit"},
    ]})

    produce = next(section for section in result["sections"] if section["section"] == "Produce")
    assert produce["items"] == [{"name": "onion", "amount": 3.0, "unit": "unit", "section": "Produce"}]


def test_convert_servings_scales_ingredients():
    result = module.convert_servings({"recipe": "salsa_verde_chicken", "current_servings": 1, "target_servings": 2})

    assert result["scale_factor"] == 2
    assert {ingredient["name"]: ingredient["amount"] for ingredient in result["ingredients"]}["frango"] == 2


def test_ingredient_substitutions_explains_matches_and_misses():
    result = module.ingredient_substitutions({"ingredients": ["chicken", "unknown"]})

    assert result["suggestions"][0]["substitutions"][0]["ingredient"] == "turkey"
    assert result["suggestions"][1]["message"] == "No suitable substitution is known."
