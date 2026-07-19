import importlib.util
import json
from pathlib import Path
import sys


SRC_ROOT = Path(__file__).resolve().parents[3]
VECTOR_PATH = SRC_ROOT / "ai" / "VectorStores" / "Python" / "local_vector_store.py"
SEMANTIC_PATH = SRC_ROOT / "ai" / "SemanticSearch" / "Python" / "semantic_search_service.py"


def load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise ImportError(f"Unable to load {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


vector_module = load_module("local_vector_store", VECTOR_PATH)
semantic_module = load_module("semantic_search_service", SEMANTIC_PATH)


class StubEmbeddingService:
    def generate_embedding(self, text: str):
        return type("Embedding", (), {"embedding": [1.0, 0.0]})()


class StubRepository:
    def get_all_recipes_with_details(self):
        return [
            {"id": 1, "name": "Spicy Chicken", "notes": "peppery dinner", "tags": ["spicy"], "prepping": "quick"},
            {"id": 2, "name": "Plain Rice", "notes": "simple side", "tags": [], "prepping": "easy"},
        ]

    def get_recipe_ingredients(self, recipe_id):
        return [{"name": "chicken"}] if recipe_id == 1 else [{"name": "rice"}]


def write_embedding(directory: Path, recipe_id: str, embedding: list[float]):
    (directory / f"{recipe_id}.json").write_text(json.dumps({
        "recipeId": recipe_id,
        "model": "test",
        "dimensions": len(embedding),
        "embedding": embedding,
    }), encoding="utf-8")


def test_cosine_similarity():
    assert vector_module.cosine_similarity([1, 0], [1, 0]) == 1
    assert vector_module.cosine_similarity([1, 0], [0, 1]) == 0
    assert vector_module.cosine_similarity([1, 0], []) == 0


def test_local_vector_store_ranking_top_k_and_cache(tmp_path):
    write_embedding(tmp_path, "1", [1.0, 0.0])
    write_embedding(tmp_path, "2", [0.0, 1.0])
    store = vector_module.LocalVectorStore(tmp_path)

    matches = store.search([1.0, 0.0], 1)
    (tmp_path / "1.json").unlink()
    cached_matches = store.search([1.0, 0.0], 2)

    assert [match.recipe_id for match in matches] == ["1"]
    assert [match.recipe_id for match in cached_matches] == ["1", "2"]


def test_empty_embedding_index_returns_no_matches(tmp_path):
    assert vector_module.LocalVectorStore(tmp_path).search([1.0, 0.0], 5) == []


def test_semantic_search_service_returns_rich_ranked_results(tmp_path):
    write_embedding(tmp_path, "1", [1.0, 0.0])
    write_embedding(tmp_path, "2", [0.0, 1.0])
    service = semantic_module.SemanticSearchService(
        StubEmbeddingService(),
        vector_module.LocalVectorStore(tmp_path),
        semantic_module.RecipeMetadataProvider(StubRepository()),
    )

    results = service.search("spicy dinner", 2)

    assert [result.recipeId for result in results] == ["1", "2"]
    assert results[0].title == "Spicy Chicken"
    assert results[0].score == 1
    assert "chicken" in results[0].matchedText
    assert "High semantic similarity" in results[0].reason


def test_unknown_or_blank_queries_return_empty(tmp_path):
    service = semantic_module.SemanticSearchService(
        StubEmbeddingService(),
        vector_module.LocalVectorStore(tmp_path),
        semantic_module.RecipeMetadataProvider(StubRepository()),
    )

    assert service.search(" ", 5) == []
    assert service.search("anything", 0) == []