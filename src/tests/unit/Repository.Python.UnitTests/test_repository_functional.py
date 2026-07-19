import sqlite3
from pathlib import Path
import importlib.util

REPO_PATH = Path(__file__).resolve().parents[3] / 'repositories' / 'Repository.Python' / '__init__.py'
repo_spec = importlib.util.spec_from_file_location('repository_python', REPO_PATH)
if repo_spec is None or repo_spec.loader is None:
    raise ImportError(f'Unable to load repository module from {REPO_PATH}')

repo_module = importlib.util.module_from_spec(repo_spec)
repo_spec.loader.exec_module(repo_module)
Repository = repo_module.Repository


def _create_test_db(db_path: Path) -> None:
    conn = sqlite3.connect(db_path)
    try:
        conn.execute(
            '''
            CREATE TABLE recipes (
              id INTEGER PRIMARY KEY,
              name TEXT,
              source_path TEXT,
              tags TEXT,
              servings INTEGER,
              time_to_prepare INTEGER,
              prepping TEXT,
              freezing_notes TEXT,
              reheat_notes TEXT,
              combinations TEXT,
              notes TEXT
            )
            '''
        )
        conn.execute(
            '''
            CREATE TABLE ingredients (
              id INTEGER PRIMARY KEY,
              name TEXT
            )
            '''
        )
        conn.execute(
            '''
            CREATE TABLE recipe_ingredients (
              recipe_id INTEGER,
              ingredient_id INTEGER,
              amount REAL,
              unit TEXT
            )
            '''
        )

        conn.execute(
            "INSERT INTO recipes VALUES (1, 'Chicken Rice', 'proteins/chicken_rice.md', 'chicken,rice', 2, 25, '', '', '', '', '')"
        )
        conn.execute("INSERT INTO ingredients VALUES (1, 'chicken')")
        conn.execute("INSERT INTO ingredients VALUES (2, 'rice')")
        conn.execute("INSERT INTO recipe_ingredients VALUES (1, 1, 200, 'g')")
        conn.execute("INSERT INTO recipe_ingredients VALUES (1, 2, 100, 'g')")
        conn.commit()
    finally:
        conn.close()


def test_get_recipe_by_id_returns_full_recipe(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    _create_test_db(db_path)
    repository = Repository(db_path=db_path)

    recipe = repository.get_recipe_by_id(1)

    assert recipe is not None
    assert recipe['name'] == 'Chicken Rice'
    assert recipe['servings'] == 2


def test_search_recipes_by_ingredients_returns_match(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    _create_test_db(db_path)
    repository = Repository(db_path=db_path)

    recipes = repository.search_recipes_by_ingredients(['chicken'])

    assert len(recipes) == 1
    assert recipes[0]['id'] == 1
    assert 'chicken' in [item.lower() for item in recipes[0]['matched_ingredients']]


def test_get_recipe_ingredients_returns_rows(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    _create_test_db(db_path)
    repository = Repository(db_path=db_path)

    ingredients = repository.get_recipe_ingredients(1)

    assert len(ingredients) == 2
    assert ingredients[0]['unit'] == 'g'


def test_get_ingredient_by_name_is_case_insensitive(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    _create_test_db(db_path)
    repository = Repository(db_path=db_path)

    ingredient = repository.get_ingredient_by_name('CHICKEN')

    assert ingredient is not None
    assert ingredient['name'] == 'chicken'
