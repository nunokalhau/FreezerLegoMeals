from pathlib import Path

from repository_test_helper import create_repository_test_db, load_repository_module


repo_module = load_repository_module()
Repository = repo_module.Repository


def test_repository_default_db_path_points_to_project_data():
    repository = Repository()

    assert repository.db_path.name == 'recipes_local.db'
    assert repository.db_path.parent.name == 'data'


def test_repository_custom_db_path_is_resolved(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    repository = Repository(db_path=db_path)

    assert repository.db_path == db_path.resolve()


def test_get_all_recipes_returns_recipe_summaries(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    create_repository_test_db(db_path)
    repository = Repository(db_path=db_path)

    recipes = repository.get_all_recipes()

    assert [recipe['name'] for recipe in recipes] == ['Chicken Rice', 'Vegetable Soup']
    assert recipes[0]['source_path'] == 'proteins/chicken_rice.md'


def test_get_recipes_alias_returns_detailed_recipes(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    create_repository_test_db(db_path)
    repository = Repository(db_path=db_path)

    recipes = repository.get_recipes()

    assert len(recipes) == 2
    assert recipes[0]['tags'] == ['chicken', 'rice']
    assert recipes[0]['combinations'] == ['Dinner Combo']
    assert recipes[1]['servings'] == 1
    assert recipes[1]['time_to_prepare'] == 0


def test_get_recipe_details_supports_lookup_by_id(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    create_repository_test_db(db_path)
    repository = Repository(db_path=db_path)

    details = repository.get_recipe_details('1')

    assert len(details) == 1
    assert details[0]['name'] == 'Chicken Rice'


def test_get_recipe_details_supports_lookup_by_name(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    create_repository_test_db(db_path)
    repository = Repository(db_path=db_path)

    details = repository.get_recipe_details('Vegetable Soup')

    assert len(details) == 1
    assert details[0]['id'] == 2


def test_missing_database_returns_empty_results_or_none(tmp_path: Path):
    db_path = tmp_path / 'missing.db'
    repository = Repository(db_path=db_path)

    assert repository.get_all_recipes() == []
    assert repository.get_recipe_by_id(1) is None
    assert repository.get_recipe_details('1') == []