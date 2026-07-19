from pathlib import Path

from repository_test_helper import create_repository_test_db, load_repository_module


repo_module = load_repository_module()
Repository = repo_module.Repository


def test_get_recipe_by_id_returns_full_recipe_fields(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    create_repository_test_db(db_path)
    repository = Repository(db_path=db_path)

    recipe = repository.get_recipe_by_id(1)

    assert recipe is not None
    assert recipe['freezing_notes'] == 'freeze well'
    assert recipe['reheat_notes'] == 'reheat gently'
    assert recipe['notes'] == 'family meal'


def test_get_all_ingredients_returns_mapping(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    create_repository_test_db(db_path)
    repository = Repository(db_path=db_path)

    ingredients = repository.get_all_ingredients()

    assert ingredients == {1: 'chicken', 3: 'onion', 2: 'rice'}


def test_get_ingredients_returns_object_list(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    create_repository_test_db(db_path)
    repository = Repository(db_path=db_path)

    ingredients = repository.get_ingredients()

    assert {'id': 1, 'name': 'chicken'} in ingredients
    assert {'id': 2, 'name': 'rice'} in ingredients


def test_get_recipe_combinations_returns_recipes_by_position(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    create_repository_test_db(db_path)
    repository = Repository(db_path=db_path)

    combinations = repository.get_recipe_combinations()

    assert 1 in combinations
    assert [recipe['name'] for recipe in combinations[1]['recipes']] == ['Chicken Rice', 'Vegetable Soup']


def test_get_combinations_returns_list_shape(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    create_repository_test_db(db_path)
    repository = Repository(db_path=db_path)

    combinations = repository.get_combinations()

    assert len(combinations) == 1
    assert combinations[0]['name'] == 'Dinner Combo'
    assert len(combinations[0]['recipes']) == 2


def test_get_combination_by_id_returns_specific_combination(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    create_repository_test_db(db_path)
    repository = Repository(db_path=db_path)

    combination = repository.get_combination_by_id(1)

    assert combination is not None
    assert combination['description'] == 'Main plus soup'


def test_get_combination_by_id_returns_none_for_invalid_id(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    create_repository_test_db(db_path)
    repository = Repository(db_path=db_path)

    assert repository.get_combination_by_id(0) is None
    assert repository.get_combination_by_id(999) is None


def test_get_ingredient_by_name_is_trimmed_and_case_insensitive(tmp_path: Path):
    db_path = tmp_path / 'recipes.db'
    create_repository_test_db(db_path)
    repository = Repository(db_path=db_path)

    ingredient = repository.get_ingredient_by_name('  ONION  ')

    assert ingredient == {'id': 3, 'name': 'onion'}