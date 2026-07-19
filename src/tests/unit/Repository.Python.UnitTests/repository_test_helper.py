import importlib.util
import sqlite3
from pathlib import Path


REPO_PATH = Path(__file__).resolve().parents[3] / 'repositories' / 'Repository.Python' / '__init__.py'


def load_repository_module():
    repo_spec = importlib.util.spec_from_file_location('repository_python', REPO_PATH)
    if repo_spec is None or repo_spec.loader is None:
        raise ImportError(f'Unable to load repository module from {REPO_PATH}')

    repo_module = importlib.util.module_from_spec(repo_spec)
    repo_spec.loader.exec_module(repo_module)
    return repo_module


def create_repository_test_db(db_path: Path) -> None:
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
              id INTEGER PRIMARY KEY,
              recipe_id INTEGER,
              ingredient_id INTEGER,
              amount REAL,
              unit TEXT
            )
            '''
        )
        conn.execute(
            '''
            CREATE TABLE recipe_combinations (
              id INTEGER PRIMARY KEY,
              name TEXT,
              description TEXT
            )
            '''
        )
        conn.execute(
            '''
            CREATE TABLE recipe_combination_items (
              id INTEGER PRIMARY KEY,
              combination_id INTEGER,
              recipe_id INTEGER,
              position INTEGER
            )
            '''
        )

        conn.execute(
            "INSERT INTO recipes VALUES (1, 'Chicken Rice', 'proteins/chicken_rice.md', 'chicken,rice', 2, 25, 'prep chicken', 'freeze well', 'reheat gently', 'Dinner Combo', 'family meal')"
        )
        conn.execute(
            "INSERT INTO recipes VALUES (2, 'Vegetable Soup', 'veggies/vegetable_soup.md', '', NULL, NULL, 'chop veg', '', '', '', '')"
        )

        conn.execute("INSERT INTO ingredients VALUES (1, 'chicken')")
        conn.execute("INSERT INTO ingredients VALUES (2, 'rice')")
        conn.execute("INSERT INTO ingredients VALUES (3, 'onion')")

        conn.execute("INSERT INTO recipe_ingredients VALUES (1, 1, 1, 200, 'g')")
        conn.execute("INSERT INTO recipe_ingredients VALUES (2, 1, 2, 100, 'g')")
        conn.execute("INSERT INTO recipe_ingredients VALUES (3, 2, 3, 2, 'pcs')")

        conn.execute("INSERT INTO recipe_combinations VALUES (1, 'Dinner Combo', 'Main plus soup')")
        conn.execute("INSERT INTO recipe_combination_items VALUES (1, 1, 1, 1)")
        conn.execute("INSERT INTO recipe_combination_items VALUES (2, 1, 2, 2)")

        conn.commit()
    finally:
        conn.close()