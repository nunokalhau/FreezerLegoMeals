from __future__ import annotations

from collections import defaultdict
from difflib import SequenceMatcher
from pathlib import Path
from typing import Any
import json
import math
import re
import sqlite3
import importlib.util
import sys

ROOT = Path(__file__).resolve().parents[2]
DATA_DIR = ROOT / "data"
FOOD_DIR = DATA_DIR / "food"
DB_PATH = DATA_DIR / "recipes_local.db"
SCHEMA_PATH = DATA_DIR / "recipes.sqlite.sql"
SEED_PATH = DATA_DIR / "recipes_manual_seed.sql"
EMBEDDINGS_DIR = DATA_DIR / "embeddings"
SRC_ROOT = ROOT / "src"

MEAT_TERMS = {"beef", "chicken", "pork", "turkey", "duck", "lamb", "fish", "salmon", "shrimp", "tuna", "bacon", "sausage", "frango", "vaca", "porco", "peru", "peixe", "camarao", "camarão", "carne"}
DAIRY_TERMS = {"milk", "cream", "cheese", "butter", "yogurt", "parmesan", "mozzarella", "leite", "natas", "queijo", "manteiga", "iogurte"}
GLUTEN_TERMS = {"wheat", "flour", "bread", "pasta", "noodle", "seitan", "tortilla", "trigo", "farinha", "pao", "pão", "massa"}
PROTEIN_TERMS = {"chicken", "beef", "pork", "turkey", "tofu", "egg", "bean", "lentil", "fish", "salmon", "shrimp", "frango", "vaca", "porco", "peru", "ovo", "feijao", "feijão", "lentilha", "peixe"}
SPICY_TERMS = {"chili", "chilli", "pepper", "jalapeno", "sriracha", "spicy", "malagueta"}
LOW_CALORIE_TERMS = {"salad", "soup", "vegetable", "veg", "broth", "lean"}

SUBSTITUTIONS = {
    "chicken": [("turkey", "similar lean protein and texture"), ("tofu", "neutral protein for vegetarian versions")],
    "beef": [("turkey", "lighter ground-meat option"), ("lentils", "keeps hearty texture for vegetarian meals")],
    "pork": [("chicken", "milder protein with similar cooking behavior"), ("mushrooms", "adds savory depth without meat")],
    "tofu": [("tempeh", "firmer plant protein"), ("chickpeas", "plant protein that holds shape")],
    "cream": [("coconut milk", "keeps richness and body"), ("cashew cream", "dairy-free creamy texture")],
    "milk": [("oat milk", "neutral dairy-free liquid"), ("soy milk", "higher-protein dairy-free liquid")],
    "butter": [("olive oil", "keeps fat and cooking function"), ("vegan butter", "closest one-for-one replacement")],
    "rice": [("quinoa", "similar base with more protein"), ("cauliflower rice", "lower-calorie base")],
    "pasta": [("rice noodles", "gluten-free noodle option"), ("zucchini noodles", "lighter vegetable option")],
}

SECTION_KEYWORDS = {
    "Produce": ["onion", "garlic", "carrot", "pepper", "tomato", "lettuce", "spinach", "mushroom", "lime", "lemon", "mango", "rutabaga", "daikon", "pineapple"],
    "Meat & Seafood": ["chicken", "beef", "pork", "turkey", "fish", "salmon", "shrimp", "bacon", "sausage"],
    "Dairy & Eggs": ["milk", "cream", "cheese", "butter", "yogurt", "egg"],
    "Pantry": ["rice", "bean", "lentil", "pasta", "flour", "sugar", "oil", "vinegar", "salt", "spice", "sauce"],
    "Frozen": ["frozen"],
}

RECIPE_ROLES = {
    "CompleteMeal",
    "MainCourse",
    "SideDish",
    "Sauce",
    "Condiment",
    "Base",
    "Snack",
    "Dessert",
    "Breakfast",
}

COMPLEMENTARY_ROLES = {"SideDish", "Sauce", "Condiment", "Base"}
DEFAULT_PRIMARY_MEAL_ROLES = ["CompleteMeal", "MainCourse"]
MEAL_TYPE_PRIMARY_ROLES = {
    "breakfast": ["Breakfast", "CompleteMeal", "MainCourse"],
    "lunch": ["CompleteMeal", "MainCourse"],
    "dinner": ["CompleteMeal", "MainCourse"],
    "snack": ["Snack", "Dessert", "CompleteMeal", "MainCourse"],
    "prep": ["CompleteMeal", "MainCourse"],
}


def search_recipes(parameters: dict[str, Any]) -> dict[str, Any]:
    recipes = _recipes_with_ingredients()
    scored = []
    for recipe in recipes:
        if _matches_recipe(recipe, parameters):
            scored.append({"recipe": recipe, "relevance": _relevance(recipe, parameters)})

    sort_by = str(parameters.get("sort_by") or "relevance").lower().replace("-", "_")
    if sort_by in {"preparation_time", "total_time"}:
        scored.sort(key=lambda item: _time(item["recipe"]))
    elif sort_by == "alphabetical":
        scored.sort(key=lambda item: item["recipe"].get("name") or "")
    elif sort_by == "newest":
        scored.sort(key=lambda item: item["recipe"].get("id") or 0, reverse=True)
    else:
        scored.sort(key=lambda item: (-item["relevance"], item["recipe"].get("name") or ""))

    limit = max(0, _int(parameters.get("limit"), len(scored)))
    return {
        "total_recipes_found": len(scored),
        "recipes": [_recipe_card(item["recipe"], item["relevance"]) for item in scored[:limit]],
        "filters": parameters,
    }


def plan_weekly_meals(parameters: dict[str, Any]) -> dict[str, Any]:
    days = max(1, min(_int(parameters.get("number_of_days"), 7), 14))
    meals_per_day = max(1, min(_int(parameters.get("meals_per_day"), 2), 5))
    filters = {
        "excluded_ingredients": parameters.get("excluded_ingredients"),
        "preparation_time": parameters.get("preparation_time_preference"),
        "limit": 100,
        **_diet_flags(parameters.get("dietary_preferences")),
    }
    candidates = search_recipes(filters)["recipes"]
    if not candidates:
        return {"days": [], "message": "No recipes matched the requested planning constraints."}

    candidates.sort(key=lambda recipe: (not recipe["freezer_friendly"], recipe["totalTime"] or 999, recipe["name"]))
    standalone_candidates = [recipe for recipe in candidates if _is_standalone_role(recipe.get("functional_role"))]
    if not standalone_candidates:
        return {
            "days": [],
            "message": "No standalone meal recipes matched the requested planning constraints.",
            "available_roles": sorted({str(recipe.get("functional_role") or "") for recipe in candidates if recipe.get("functional_role")}),
        }

    complements = [recipe for recipe in candidates if _is_complementary_role(recipe.get("functional_role"))]
    plan = []
    role_cursors = defaultdict(int)
    complement_cursors = defaultdict(int)
    for day in range(1, days + 1):
        meals = []
        for slot in range(1, meals_per_day + 1):
            meal_type = _meal_type(slot)
            primary_recipe = _next_recipe_for_meal_type(meal_type, standalone_candidates, role_cursors)
            meal_entry = {"meal_type": meal_type, "recipe": primary_recipe}
            selected_complements = _select_complements_for_meal(meal_type, complements, complement_cursors)
            if selected_complements:
                meal_entry["complements"] = selected_complements
            meals.append(meal_entry)
        plan.append({"day": day, "meals": meals})

    return {
        "days": plan,
        "calorie_target": parameters.get("calorie_target"),
        "planning_notes": [
            "Standalone meals are selected from CompleteMeal/MainCourse/Breakfast/Snack/Dessert roles as appropriate for each meal slot.",
            "Sauces, condiments, side dishes, and bases are suggested only as complements.",
            "CompleteMeal is prioritized first, then MainCourse when CompleteMeal is unavailable.",
            "Repeated recipes are distributed by role until each role pool is exhausted.",
            "Freezer-friendly meals are prioritized where constraints allow.",
            "Ingredient reuse is encouraged by selecting from the same filtered recipe pool.",
        ],
    }


def get_recipe(parameters: dict[str, Any]) -> dict[str, Any]:
    recipe = _resolve_recipe(parameters.get("id") or parameters.get("name") or parameters.get("recipe"))
    if not recipe:
        return {"error": "Recipe not found."}

    return {
        "recipe": _recipe_card(recipe),
        "numeric_id": recipe.get("id"),
        "functional_role": _infer_recipe_role(recipe),
        "ingredients": recipe.get("ingredients") or [],
        "steps": _steps(recipe),
        "prepping": recipe.get("prepping"),
        "freezing_notes": recipe.get("freezing_notes"),
        "reheat_notes": recipe.get("reheat_notes"),
        "notes": recipe.get("notes"),
    }


def replace_meal(parameters: dict[str, Any]) -> dict[str, Any]:
    current = str(parameters.get("current_recipe") or "").lower()
    meal_type = str(parameters.get("meal_type") or "dinner").strip().lower()
    candidates = search_recipes({
        "excluded_ingredients": parameters.get("excluded_ingredients"),
        "preparation_time": parameters.get("preparation_time_preference"),
        "limit": 25,
        **_diet_flags(parameters.get("dietary_preferences")),
    })["recipes"]
    allowed_roles = set(MEAL_TYPE_PRIMARY_ROLES.get(meal_type, DEFAULT_PRIMARY_MEAL_ROLES))
    for recipe in candidates:
        if recipe.get("functional_role") not in allowed_roles:
            continue
        if current and current in json.dumps(recipe).lower():
            continue
        return {
            "meal_type": parameters.get("meal_type"),
            "replacement": recipe,
            "rationale": "Preserves restrictions and preparation constraints while keeping a standalone meal role appropriate for the requested slot.",
        }
    return {
        "meal_type": parameters.get("meal_type"),
        "replacement": None,
        "message": "No suitable standalone meal replacement found.",
        "allowed_roles": sorted(allowed_roles),
    }


def optimize_shopping_list(parameters: dict[str, Any]) -> dict[str, Any]:
    merged: dict[tuple[str, str], dict[str, Any]] = {}
    for item in _list(parameters.get("items")):
        item_dict = item if isinstance(item, dict) else {"name": str(item)}
        name = _normalize_ingredient(str(item_dict.get("name") or item_dict.get("ingredient") or ""))
        if not name:
            continue
        unit = str(item_dict.get("unit") or "").lower()
        amount = _float(item_dict.get("amount", item_dict.get("quantity")), 0)
        key = (name, unit)
        if key not in merged:
            merged[key] = {"name": name, "amount": 0.0, "unit": unit, "section": _section(name)}
        merged[key]["amount"] += amount

    grouped: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for item in merged.values():
        item["amount"] = _round(item["amount"])
        grouped[item["section"]].append(item)

    return {"sections": [{"section": section, "items": sorted(items, key=lambda value: value["name"])} for section, items in sorted(grouped.items())]}


def batch_cooking_plan(parameters: dict[str, Any]) -> dict[str, Any]:
    recipes = [_resolve_recipe(value) for value in _list(parameters.get("recipes"))]
    recipes = [recipe for recipe in recipes if recipe]
    names = [recipe["name"] for recipe in recipes]
    return {
        "recipes": names,
        "estimated_total_minutes": sum(_time(recipe) for recipe in recipes),
        "schedule": [
            {"step": 1, "task": "Read all recipes and pull shared ingredients.", "recipes": names},
            {"step": 2, "task": "Wash and chop all produce together before heating pans.", "recipes": names},
            {"step": 3, "task": "Measure pantry spices, sauces, grains, and legumes in one pass.", "recipes": names},
            {"step": 4, "task": "Cook longest grains, beans, or oven items first.", "recipes": [recipe["name"] for recipe in sorted(recipes, key=_time, reverse=True)]},
            {"step": 5, "task": "Cook proteins from mildest to strongest flavor to reduce cleaning.", "recipes": names},
            {"step": 6, "task": "Assemble, cool, label, and freeze freezer-friendly meals first.", "recipes": [recipe["name"] for recipe in recipes if _flag(recipe, "freezer_friendly")]},
        ],
    }


def convert_servings(parameters: dict[str, Any]) -> dict[str, Any]:
    recipe = _resolve_recipe(parameters.get("recipe"))
    if not recipe:
        return {"error": "Recipe not found."}
    current = max(1, _int(parameters.get("current_servings"), recipe.get("servings") or 1))
    target = max(1, _int(parameters.get("target_servings"), current))
    factor = target / current
    ingredients = []
    for ingredient in recipe.get("ingredients") or []:
        amount = _float(ingredient.get("amount"), 0)
        ingredients.append({
            "name": ingredient.get("name"),
            "amount": _round(amount * factor) if amount else None,
            "unit": ingredient.get("unit"),
            "original_amount": amount or None,
        })
    return {"recipe": _recipe_card(recipe), "current_servings": current, "target_servings": target, "scale_factor": factor, "ingredients": ingredients}


def ingredient_substitutions(parameters: dict[str, Any]) -> dict[str, Any]:
    suggestions = []
    for ingredient in _strings(parameters.get("ingredients")):
        matches = SUBSTITUTIONS.get(_normalize_ingredient(ingredient))
        suggestions.append({
            "ingredient": ingredient,
            "substitutions": [] if not matches else [{"ingredient": name, "reason": reason} for name, reason in matches],
            **({"message": "No suitable substitution is known."} if not matches else {}),
        })
    return {"suggestions": suggestions, "recipe": parameters.get("recipe")}


def semantic_search(parameters: dict[str, Any]) -> list[dict[str, Any]]:
    query = str(parameters.get("query") or parameters.get("text") or "")
    top_k = max(0, _int(parameters.get("topK", parameters.get("top_k")), 5))
    if not query.strip() or top_k <= 0:
        return []

    embedding_module = _load_module("embedding_python_service", SRC_ROOT / "ai" / "Embedding.Python" / "embedding_service.py")
    vector_store_module = _load_module("semantic_chroma_vector_store", SRC_ROOT / "ai" / "VectorStores" / "Python" / "chroma_vector_store.py")
    semantic_module = _load_module("semantic_search_python_service", SRC_ROOT / "ai" / "SemanticSearch" / "Python" / "semantic_search_service.py")
    repository_module = _load_module("repository_python", SRC_ROOT / "repositories" / "Repository.Python" / "__init__.py")

    service = semantic_module.SemanticSearchService(
        embedding_module.OllamaEmbeddingService(),
        vector_store_module.ChromaVectorStore(),
        semantic_module.RecipeMetadataProvider(repository_module.Repository(DB_PATH)),
    )
    return [result.__dict__ for result in service.search(query, top_k)]


def run_tool(tool_name: str, parameters: dict[str, Any]) -> Any:
    tools = {
        "search_recipes": search_recipes,
        "plan_weekly_meals": plan_weekly_meals,
        "get_recipe": get_recipe,
        "replace_meal": replace_meal,
        "optimize_shopping_list": optimize_shopping_list,
        "batch_cooking_plan": batch_cooking_plan,
        "convert_servings": convert_servings,
        "ingredient_substitutions": ingredient_substitutions,
        "semantic_search": semantic_search,
    }
    if tool_name not in tools:
        raise ValueError(f"Unknown canonical AI tool: {tool_name}")
    return tools[tool_name](parameters or {})


def _load_module(module_name: str, path: Path):
    spec = importlib.util.spec_from_file_location(module_name, path)
    if spec is None or spec.loader is None:
        raise ImportError(f"Unable to load {module_name} from {path}")

    module = importlib.util.module_from_spec(spec)
    sys.modules[module_name] = module
    spec.loader.exec_module(module)
    return module


def _recipes_with_ingredients() -> list[dict[str, Any]]:
    _ensure_database()
    conn = sqlite3.connect(DB_PATH)
    try:
        cursor = conn.execute("""
            SELECT id, name, source_path, tags, servings, time_to_prepare, prepping, freezing_notes, reheat_notes, combinations, notes
            FROM recipes
            ORDER BY name
        """)
        recipes = []
        for row in cursor.fetchall():
            recipe = {
                "id": row[0], "name": row[1], "source_path": row[2], "tags": row[3], "servings": row[4] or 1,
                "time_to_prepare": row[5] or 0, "prepping": row[6] or "", "freezing_notes": row[7] or "",
                "reheat_notes": row[8] or "", "combinations": row[9] or "", "notes": row[10] or "",
            }
            recipe["ingredients"] = _recipe_ingredients(conn, recipe["id"])
            recipes.append(recipe)
        return recipes
    finally:
        conn.close()


def _ensure_database() -> None:
    if _has_recipes_table():
        return
    if DB_PATH.exists():
        DB_PATH.unlink()
    conn = sqlite3.connect(DB_PATH)
    try:
        conn.executescript(SCHEMA_PATH.read_text(encoding="utf-8"))
        conn.executescript(SEED_PATH.read_text(encoding="utf-8"))
        for localized_seed_path in _supplemental_seed_paths():
            conn.executescript(localized_seed_path.read_text(encoding="utf-8"))
        conn.commit()
    finally:
        conn.close()


def _supplemental_seed_paths() -> list[Path]:
    return sorted((FOOD_DIR).glob("**/*.localization.seed.sql"))


def _has_recipes_table() -> bool:
    if not DB_PATH.exists():
        return False
    conn = sqlite3.connect(DB_PATH)
    try:
        cursor = conn.execute("SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'recipes'")
        return cursor.fetchone() is not None
    finally:
        conn.close()


def _recipe_ingredients(conn: sqlite3.Connection, recipe_id: int) -> list[dict[str, Any]]:
    cursor = conn.execute("""
        SELECT i.name, ri.amount, ri.unit
        FROM recipe_ingredients ri
        JOIN ingredients i ON ri.ingredient_id = i.id
        WHERE ri.recipe_id = ?
        ORDER BY i.name
    """, (recipe_id,))
    return [{"name": row[0], "amount": row[1], "unit": row[2]} for row in cursor.fetchall()]


def _resolve_recipe(value: Any) -> dict[str, Any] | None:
    text = str(value or "").strip()
    for recipe in _recipes_with_ingredients():
        if text.isdigit() and recipe.get("id") == int(text):
            return recipe
        if recipe.get("name", "").lower() == text.lower() or _recipe_identifier(recipe).lower() == text.lower():
            return recipe
    return None


def _matches_recipe(recipe: dict[str, Any], filters: dict[str, Any]) -> bool:
    text = _recipe_text(recipe)
    ingredient_names = [_normalize_ingredient(item.get("name", "")) for item in recipe.get("ingredients") or []]
    fuzzy = _bool(filters.get("fuzzy"), True)
    if _strings(filters.get("ingredients")) and not all(_matches_text(term, text, ingredient_names, fuzzy) for term in _strings(filters.get("ingredients"))):
        return False
    if any(_matches_text(term, text, ingredient_names, fuzzy) for term in _strings(filters.get("excluded_ingredients"))):
        return False
    for key in ["category", "cuisine", "difficulty"]:
        if filters.get(key) and not _contains(text, str(filters[key]), fuzzy):
            return False
    if _strings(filters.get("tags")) and not all(_contains(text, tag, fuzzy) for tag in _strings(filters.get("tags"))):
        return False
    if filters.get("functional_role"):
        requested_roles = {role for role in (_normalize_role(value) for value in _list(filters.get("functional_role"))) if role}
        if requested_roles and _infer_recipe_role(recipe) not in requested_roles:
            return False
    if filters.get("standalone_only") is not None and _bool(filters.get("standalone_only")) and not _is_standalone_role(_infer_recipe_role(recipe)):
        return False
    max_time = _int(filters.get("preparation_time") or filters.get("cooking_time") or filters.get("total_time"), 0)
    if max_time and _time(recipe) > max_time:
        return False
    if filters.get("servings") is not None and _int(recipe.get("servings"), 0) < _int(filters.get("servings"), 0):
        return False
    for flag in ["freezer_friendly", "batch_cooking_friendly", "vegetarian", "vegan", "gluten_free", "dairy_free", "spicy", "high_protein", "low_calorie"]:
        if filters.get(flag) is not None and _bool(filters.get(flag)) != _flag(recipe, flag):
            return False
    return True


def _recipe_card(recipe: dict[str, Any], relevance: int | None = None) -> dict[str, Any]:
    prep_time = _time(recipe)
    role = _infer_recipe_role(recipe)
    card = {
        "id": _recipe_identifier(recipe),
        "name": recipe.get("name"),
        "description": recipe.get("notes") or recipe.get("freezing_notes") or "Recipe suggestion.",
        "category": _category(recipe),
        "functional_role": role,
        "is_standalone_meal": _is_standalone_role(role),
        "tags": _tags(recipe),
        "prepTime": prep_time,
        "cookTime": 0,
        "totalTime": prep_time,
        "servings": recipe.get("servings") or 1,
        "freezer_friendly": _flag(recipe, "freezer_friendly"),
        "batch_cooking_friendly": _flag(recipe, "batch_cooking_friendly"),
    }
    if relevance is not None:
        card["relevance"] = relevance
    return card


def _flag(recipe: dict[str, Any], flag: str) -> bool:
    text = _recipe_text(recipe)
    ingredients = {_normalize_ingredient(item.get("name", "")) for item in recipe.get("ingredients") or []}
    if flag == "freezer_friendly":
        return bool(str(recipe.get("freezing_notes") or "").strip()) or "freezer" in text or "freeze" in text
    if flag == "batch_cooking_friendly":
        return _flag(recipe, "freezer_friendly") or "batch" in text or _int(recipe.get("servings"), 0) >= 4
    if flag == "vegetarian":
        return not any(any(meat in ingredient for meat in MEAT_TERMS) for ingredient in ingredients)
    if flag == "vegan":
        return _flag(recipe, "vegetarian") and not any(any(dairy in ingredient for dairy in DAIRY_TERMS) for ingredient in ingredients)
    if flag == "gluten_free":
        return not any(any(gluten in ingredient for gluten in GLUTEN_TERMS) for ingredient in ingredients)
    if flag == "dairy_free":
        return not any(any(dairy in ingredient for dairy in DAIRY_TERMS) for ingredient in ingredients)
    if flag == "spicy":
        return any(term in text for term in SPICY_TERMS)
    if flag == "high_protein":
        return any(any(protein in ingredient for protein in PROTEIN_TERMS) for ingredient in ingredients)
    if flag == "low_calorie":
        return any(term in text for term in LOW_CALORIE_TERMS)
    return False


def _relevance(recipe: dict[str, Any], filters: dict[str, Any]) -> int:
    text = _recipe_text(recipe)
    return sum(3 for term in [*_strings(filters.get("ingredients")), *_strings(filters.get("tags"))] if _contains(text, term, True)) + (1 if _flag(recipe, "freezer_friendly") else 0)


def _category(recipe: dict[str, Any]) -> str:
    text = _recipe_text(recipe)
    for category, terms in {
        "Asian": ["asian", "vietnamese", "teriyaki", "soy", "rice", "noodle"],
        "Mexican": ["mexican", "taco", "salsa", "chili", "tortilla"],
        "Mediterranean": ["mediterranean", "olive", "lemon", "chickpea"],
        "Pickles": ["pickle", "pickles", "brine", "sweet-sour"],
    }.items():
        if any(term in text for term in terms):
            return category
    return "Vegetarian" if _flag(recipe, "vegetarian") else "General"


def _next_recipe_for_meal_type(
    meal_type: str,
    standalone_candidates: list[dict[str, Any]],
    role_cursors: dict[str, int],
) -> dict[str, Any]:
    preferred_roles = MEAL_TYPE_PRIMARY_ROLES.get(meal_type, DEFAULT_PRIMARY_MEAL_ROLES)
    for role in preferred_roles:
        recipes_for_role = [recipe for recipe in standalone_candidates if recipe.get("functional_role") == role]
        if recipes_for_role:
            cursor = role_cursors[role]
            selected = recipes_for_role[cursor % len(recipes_for_role)]
            role_cursors[role] = cursor + 1
            return selected

    fallback_cursor = role_cursors["_fallback"]
    selected = standalone_candidates[fallback_cursor % len(standalone_candidates)]
    role_cursors["_fallback"] = fallback_cursor + 1
    return selected


def _select_complements_for_meal(
    meal_type: str,
    complements: list[dict[str, Any]],
    complement_cursors: dict[str, int],
) -> list[dict[str, Any]]:
    if not complements:
        return []

    desired_roles_by_meal = {
        "breakfast": ["Base", "Condiment"],
        "lunch": ["Base", "SideDish", "Sauce", "Condiment"],
        "dinner": ["Base", "SideDish", "Sauce", "Condiment"],
        "snack": ["Condiment", "Sauce"],
        "prep": ["Base", "SideDish", "Sauce", "Condiment"],
    }
    desired_roles = desired_roles_by_meal.get(meal_type, ["Base", "SideDish", "Sauce", "Condiment"])

    selected: list[dict[str, Any]] = []
    for role in desired_roles:
        recipes_for_role = [recipe for recipe in complements if recipe.get("functional_role") == role]
        if not recipes_for_role:
            continue
        cursor = complement_cursors[role]
        selected.append(recipes_for_role[cursor % len(recipes_for_role)])
        complement_cursors[role] = cursor + 1
        if len(selected) >= 2:
            break

    return selected


def _infer_recipe_role(recipe: dict[str, Any]) -> str:
    tags = {_normalize_tag(tag) for tag in _strings(recipe.get("tags"))}
    explicit_tag_role_map = {
        "complete-meal": "CompleteMeal",
        "complete_meal": "CompleteMeal",
        "main-course": "MainCourse",
        "main_course": "MainCourse",
        "side-dish": "SideDish",
        "side_dish": "SideDish",
        "sauce": "Sauce",
        "condiment": "Condiment",
        "base": "Base",
        "starch": "Base",
        "snack": "Snack",
        "dessert": "Dessert",
        "breakfast": "Breakfast",
        "pickle": "Condiment",
    }
    for tag in tags:
        role = explicit_tag_role_map.get(tag)
        if role:
            return role

    source_path = str(recipe.get("source_path") or "")
    category = Path(source_path).parent.name.strip().lower() if source_path else ""
    category_role_map = {
        "combos": "CompleteMeal",
        "proteins": "MainCourse",
        "veggies": "SideDish",
        "starches": "Base",
        "sauces": "Sauce",
        "pickles": "Condiment",
        "snacks": "Snack",
        "desserts": "Dessert",
        "breakfast": "Breakfast",
    }
    if category in category_role_map:
        return category_role_map[category]

    return "MainCourse"


def _normalize_role(value: Any) -> str | None:
    text = str(value or "").strip()
    if not text:
        return None
    compact = re.sub(r"[^a-zA-Z]", "", text).lower()
    for role in RECIPE_ROLES:
        if compact == re.sub(r"[^a-zA-Z]", "", role).lower():
            return role
    return None


def _is_standalone_role(role: Any) -> bool:
    normalized = _normalize_role(role)
    if not normalized:
        return False
    return normalized not in COMPLEMENTARY_ROLES


def _is_complementary_role(role: Any) -> bool:
    normalized = _normalize_role(role)
    if not normalized:
        return False
    return normalized in COMPLEMENTARY_ROLES


def _tags(recipe: dict[str, Any]) -> list[str]:
    tags = _strings(recipe.get("tags"))
    for flag, tag in [("freezer_friendly", "freezer-friendly"), ("batch_cooking_friendly", "batch-cooking"), ("vegetarian", "vegetarian"), ("vegan", "vegan"), ("gluten_free", "gluten-free"), ("dairy_free", "dairy-free"), ("spicy", "spicy"), ("high_protein", "high-protein"), ("low_calorie", "low-calorie")]:
        if _flag(recipe, flag):
            tags.append(tag)
    return sorted({_normalize_tag(tag) for tag in tags if tag})


def _recipe_identifier(recipe: dict[str, Any]) -> str:
    source_path = str(recipe.get("source_path") or "")
    if source_path:
        return Path(source_path).stem
    return re.sub(r"[^a-z0-9]+", "-", str(recipe.get("name") or "").lower()).strip("-") or str(recipe.get("id"))


def _recipe_text(recipe: dict[str, Any]) -> str:
    ingredients = " ".join(str(item.get("name") or "") for item in recipe.get("ingredients") or [])
    return " ".join(str(value) for value in [recipe.get("name"), recipe.get("source_path"), recipe.get("tags"), recipe.get("prepping"), recipe.get("freezing_notes"), recipe.get("notes"), ingredients] if value).lower()


def _steps(recipe: dict[str, Any]) -> list[str]:
    return [step.strip(" -0123456789.") for step in re.split(r"\n+|\d+\.\s+", str(recipe.get("prepping") or "")) if step.strip(" -0123456789.")]


def _matches_text(term: str, text: str, ingredients: list[str], fuzzy: bool) -> bool:
    normalized = _normalize_ingredient(term)
    return _contains(text, normalized, fuzzy) or any(_contains(ingredient, normalized, fuzzy) for ingredient in ingredients)


def _contains(text: str, term: str, fuzzy: bool) -> bool:
    normalized = str(term or "").strip().lower()
    if not normalized or normalized in text:
        return True
    return fuzzy and any(SequenceMatcher(None, normalized, word).ratio() >= 0.82 for word in re.findall(r"[a-zA-ZÀ-ÿ]+", text))


def _diet_flags(preferences: Any) -> dict[str, bool]:
    flags = {}
    for preference in _strings(preferences):
        key = preference.lower().replace(" ", "_").replace("-", "_")
        if key in {"vegetarian", "vegan", "gluten_free", "dairy_free", "high_protein", "low_calorie"}:
            flags[key] = True
    return flags


def _section(name: str) -> str:
    for section, keywords in SECTION_KEYWORDS.items():
        if any(keyword in name for keyword in keywords):
            return section
    return "Other"


def _meal_type(slot: int) -> str:
    return ["breakfast", "lunch", "dinner", "snack", "prep"][min(slot - 1, 4)]


def _time(recipe: dict[str, Any]) -> int:
    return _int(recipe.get("time_to_prepare"), 0)


def _normalize_ingredient(value: str) -> str:
    value = re.sub(r"\b(fresh|frozen|dried|ground|chopped|sliced|minced|large|small|medium)\b", "", value.lower())
    value = re.sub(r"[^a-zA-ZÀ-ÿ0-9 ]+", " ", value)
    return re.sub(r"\s+", " ", value).strip()


def _normalize_tag(value: str) -> str:
    return re.sub(r"\s+", "-", str(value).strip().lower().replace("_", "-"))


def _strings(value: Any) -> list[str]:
    return [str(item).strip() for item in _list(value) if str(item).strip()]


def _list(value: Any) -> list[Any]:
    if value is None:
        return []
    if isinstance(value, list):
        return value
    if isinstance(value, tuple):
        return list(value)
    if isinstance(value, str):
        return [item.strip() for item in value.split(",") if item.strip()]
    return [value]


def _bool(value: Any, default: bool = False) -> bool:
    if value is None:
        return default
    if isinstance(value, bool):
        return value
    return str(value).strip().lower() in {"true", "1", "yes", "y"}


def _int(value: Any, default: int) -> int:
    try:
        return int(float(value))
    except (TypeError, ValueError):
        return default


def _float(value: Any, default: float) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def _round(value: float) -> float:
    if value == 0:
        return 0
    if abs(value) < 1:
        return round(value, 3)
    if abs(value) < 10:
        return round(value, 2)
    return round(value, 1)
