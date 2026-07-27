import importlib.util
import json
from pathlib import Path
import sys
from types import SimpleNamespace

import pytest


SRC_ROOT = Path(__file__).resolve().parents[3]
VECTOR_PATH = SRC_ROOT / "ai" / "VectorStores" / "Python" / "chroma_vector_store.py"
SEMANTIC_PATH = SRC_ROOT / "ai" / "SemanticSearch" / "Python" / "semantic_search_service.py"


def load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise ImportError(f"Unable to load {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


vector_module = load_module("chroma_vector_store", VECTOR_PATH)
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


class FakeHttpResponse:
    def __init__(self, payload: dict):
        self._payload = payload

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        return False

    def read(self):
        return json.dumps(self._payload).encode("utf-8")


def build_store(collection_name: str = "recipe_embeddings"):
    return vector_module.ChromaVectorStore(
        vector_module.ChromaVectorStoreOptions(
            base_url="http://localhost:8001",
            tenant="default_tenant",
            database="default_database",
            collection_name=collection_name,
            timeout_seconds=5,
        )
    )


def test_cosine_similarity():
    assert vector_module.cosine_similarity([1, 0], [1, 0]) == 1
    assert vector_module.cosine_similarity([1, 0], [0, 1]) == 0
    assert vector_module.cosine_similarity([1, 0], []) == 0


def test_chroma_vector_store_ranking_top_k_and_collection_cache(monkeypatch):
    create_calls = 0
    query_calls = 0
    query_responses = [
        {
            "ids": [["1"]],
            "embeddings": [[[1.0, 0.0]]],
            "distances": [[0.0]],
            "include": ["embeddings", "distances"],
        },
        {
            "ids": [["1", "2"]],
            "embeddings": [[[1.0, 0.0], [0.0, 1.0]]],
            "distances": [[0.0, 1.0]],
            "include": ["embeddings", "distances"],
        },
    ]

    def fake_urlopen(req, timeout):
        nonlocal create_calls
        nonlocal query_calls
        assert timeout == 5
        body = json.loads(req.data.decode("utf-8"))
        if req.full_url.endswith("/collections"):
            create_calls += 1
            assert body["name"] == "recipe_embeddings"
            assert body["get_or_create"] is True
            return FakeHttpResponse({"id": "collection-1"})

        if req.full_url.endswith("/query"):
            query_calls += 1
            assert body["query_embeddings"] == [[1.0, 0.0]]
            assert body["n_results"] in (1, 2)
            return FakeHttpResponse(query_responses.pop(0))

        raise AssertionError(f"Unexpected request {req.full_url}")

    monkeypatch.setattr(vector_module.request, "urlopen", fake_urlopen)
    store = build_store()

    matches = store.search([1.0, 0.0], 1)
    cached_matches = store.search([1.0, 0.0], 2)

    assert [match.recipe_id for match in matches] == ["1"]
    assert [match.recipe_id for match in cached_matches] == ["1", "2"]
    assert create_calls == 1
    assert query_calls == 2


def test_chroma_vector_store_returns_empty_for_empty_index(monkeypatch):
    def fake_urlopen(req, timeout):
        if req.full_url.endswith("/collections"):
            return FakeHttpResponse({"id": "collection-1"})
        if req.full_url.endswith("/query"):
            return FakeHttpResponse({"ids": [[]], "embeddings": [[]], "distances": [[]]})
        raise AssertionError(f"Unexpected request {req.full_url}")

    monkeypatch.setattr(vector_module.request, "urlopen", fake_urlopen)
    assert build_store().search([1.0, 0.0], 5) == []


def test_chroma_vector_store_with_missing_collection_name_raises_runtime_error():
    with pytest.raises(RuntimeError, match="collection name"):
        build_store(collection_name=" ")


def test_chroma_vector_store_when_collection_create_has_no_id_raises_runtime_error(monkeypatch):
    monkeypatch.setattr(vector_module.request, "urlopen", lambda req, timeout: FakeHttpResponse({"name": "recipe_embeddings"}))

    with pytest.raises(RuntimeError, match="did not include an id"):
        build_store().search([1.0, 0.0], 1)


def test_chroma_vector_store_when_embeddings_missing_uses_distance_fallback(monkeypatch):
    def fake_urlopen(req, timeout):
        if req.full_url.endswith("/collections"):
            return FakeHttpResponse({"id": "collection-1"})
        if req.full_url.endswith("/query"):
            return FakeHttpResponse({
                "ids": [["2", "1"]],
                "embeddings": None,
                "distances": [[0.6, 0.1]],
                "include": ["distances"],
            })
        raise AssertionError(f"Unexpected request {req.full_url}")

    monkeypatch.setattr(vector_module.request, "urlopen", fake_urlopen)
    matches = build_store().search([1.0, 0.0], 2)

    assert [match.recipe_id for match in matches] == ["1", "2"]
    assert matches[0].score == pytest.approx(0.9, rel=1e-5)
    assert matches[1].score == pytest.approx(0.4, rel=1e-5)


def test_semantic_search_service_returns_rich_ranked_results():
    service = semantic_module.SemanticSearchService(
        StubEmbeddingService(),
        SimpleNamespace(search=lambda _embedding, _top_k: [
            vector_module.VectorMatch(recipe_id="1", score=1.0),
            vector_module.VectorMatch(recipe_id="2", score=0.0),
        ]),
        semantic_module.RecipeMetadataProvider(StubRepository()),
    )

    results = service.search("spicy dinner", 2)

    assert [result.recipeId for result in results] == ["1", "2"]
    assert results[0].title == "Spicy Chicken"
    assert results[0].score == 1
    assert "chicken" in results[0].matchedText
    assert "High semantic similarity" in results[0].reason


def test_unknown_or_blank_queries_return_empty():
    service = semantic_module.SemanticSearchService(
        StubEmbeddingService(),
        SimpleNamespace(search=lambda _embedding, _top_k: []),
        semantic_module.RecipeMetadataProvider(StubRepository()),
    )

    assert service.search(" ", 5) == []
    assert service.search("anything", 0) == []