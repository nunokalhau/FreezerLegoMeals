import importlib.util
from pathlib import Path
import sys


SRC_ROOT = Path(__file__).resolve().parents[3]
PIPELINE_PATH = SRC_ROOT / "scripts" / "embeddings"
PYTHON_EMBEDDING_PATH = SRC_ROOT / "ai" / "Embedding.Python"


def load_module(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, filename)
    if spec is None or spec.loader is None:
        raise ImportError(f"Unable to load {filename}")

    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


if str(PIPELINE_PATH) not in sys.path:
    sys.path.insert(0, str(PIPELINE_PATH))

embedding_service_module = load_module("embedding_service", PYTHON_EMBEDDING_PATH / "embedding_service.py")
builder_module = load_module("recipe_embedding_builder", PIPELINE_PATH / "recipe_embedding_builder.py")
indexer_module = load_module("embedding_indexer", PIPELINE_PATH / "embedding_indexer.py")

EmbeddingResponse = embedding_service_module.EmbeddingResponse
RecipeEmbeddingBuilder = builder_module.RecipeEmbeddingBuilder
EmbeddingIndexer = indexer_module.EmbeddingIndexer


class StubEmbeddingService:
    def generate_embedding(self, text: str):
        assert "Title: Chicken Curry" in text
        return EmbeddingResponse(model="test-model", dimensions=3, embedding=[0.1, 0.2, 0.3])


def test_recipe_embedding_builder_creates_single_semantic_document():
    recipe = {
        "id": 7,
        "name": "Chicken Curry",
        "notes": "Freezer-friendly curry",
        "tags": "chicken, curry",
        "time_to_prepare": 45,
        "prepping": "Slice chicken",
        "freezing_notes": "Freeze flat",
        "reheat_notes": "Simmer until hot",
        "recipe_ingredients": [
            {"name": "chicken", "amount": 2, "unit": "lb"},
            {"ingredient": {"name": "rice"}},
        ],
    }

    document = RecipeEmbeddingBuilder().build_document(recipe)

    assert "Title: Chicken Curry" in document
    assert "Description: Freezer-friendly curry" in document
    assert "Tags: chicken, curry" in document
    assert "Ingredients: 2 lb chicken, rice" in document
    assert "Preparation: Slice chicken" in document
    assert "Cooking time: 45" in document
    assert "Instructions: Freeze flat" in document
    assert "Simmer until hot" in document


def test_recipe_embedding_indexer_writes_one_file_per_recipe(tmp_path):
    recipes = [{"id": 7, "name": "Chicken Curry"}]

    count = EmbeddingIndexer(StubEmbeddingService()).index_recipes(recipes, tmp_path)

    assert count == 1
    output = (tmp_path / "7.json").read_text(encoding="utf-8")
    assert '"recipeId": "7"' in output
    assert '"model": "test-model"' in output
    assert '"dimensions": 3' in output