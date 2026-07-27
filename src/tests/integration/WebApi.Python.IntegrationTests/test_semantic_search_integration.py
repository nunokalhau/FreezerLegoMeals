#!/usr/bin/env python3
"""Integration tests for Python semantic search endpoint with real ChromaDB flow."""

import asyncio
import importlib.util
import json
import uuid
from pathlib import Path
from urllib import parse, request
from urllib.error import URLError

import httpx
import pytest


SRC_ROOT = Path(__file__).resolve().parents[3]
APP_PATH = SRC_ROOT / "api" / "WebApi.Python" / "app.py"
CHROMA_BASE_URL = "http://localhost:8001"
TENANT = "default_tenant"
DATABASE = "default_database"


def load_app_module(module_name: str):
    app_spec = importlib.util.spec_from_file_location(module_name, APP_PATH)
    if app_spec is None or app_spec.loader is None:
        raise ImportError(f"Unable to load app module from {APP_PATH}")

    app_module = importlib.util.module_from_spec(app_spec)
    app_spec.loader.exec_module(app_module)
    return app_module


def chroma_available_or_skip() -> None:
    try:
        with request.urlopen(f"{CHROMA_BASE_URL}/api/v2/heartbeat", timeout=5) as response:
            if response.status < 200 or response.status >= 300:
                pytest.skip(f"Local ChromaDB is required at {CHROMA_BASE_URL}; /api/v2/heartbeat returned {response.status}.")
    except (TimeoutError, URLError) as error:
        pytest.skip(f"Local ChromaDB is required at {CHROMA_BASE_URL}; heartbeat failed: {error}")


def test_semantic_search_endpoint_uses_chromadb_collection_create_and_reuse(monkeypatch):
    chroma_available_or_skip()

    collection_name = f"itest_recipe_embeddings_{uuid.uuid4().hex}"
    monkeypatch.setenv("CHROMA_VECTOR_STORE_BASE_URL", CHROMA_BASE_URL)
    monkeypatch.setenv("CHROMA_VECTOR_STORE_TENANT", TENANT)
    monkeypatch.setenv("CHROMA_VECTOR_STORE_DATABASE", DATABASE)
    monkeypatch.setenv("CHROMA_VECTOR_STORE_COLLECTION_NAME", collection_name)
    monkeypatch.setenv("CHROMA_VECTOR_STORE_TIMEOUT_SECONDS", "15")

    app_module = load_app_module("webapi_python_semantic_chroma_integration_1")
    app_module.embedding_service.generate_embedding = lambda _query: type("Embedding", (), {"embedding": [1.0, 0.0]})()

    collection_id = None
    try:
        initial = asyncio.run(post_semantic_search(app_module.app, "spicy", 2))
        assert initial.status_code in range(200, 300)
        assert initial.json() == []

        collection_id = get_collection_id_by_name(collection_name)
        assert collection_id

        recipe_ids = asyncio.run(find_existing_recipe_ids(app_module.app, count=2))
        if len(recipe_ids) < 2:
            pytest.skip("At least two recipes are required for semantic search integration assertions.")

        upsert_embeddings(collection_id, [str(recipe_ids[0]), str(recipe_ids[1])], [[1.0, 0.0], [0.0, 1.0]])

        first_results = asyncio.run(post_semantic_search(app_module.app, "spicy", 2))
        assert first_results.status_code in range(200, 300)
        first_payload = first_results.json()
        assert [item["recipeId"] for item in first_payload] == [str(recipe_ids[0]), str(recipe_ids[1])]
        assert first_payload[0]["score"] >= first_payload[1]["score"]

        app_module_second = load_app_module("webapi_python_semantic_chroma_integration_2")
        app_module_second.embedding_service.generate_embedding = lambda _query: type("Embedding", (), {"embedding": [1.0, 0.0]})()
        second_results = asyncio.run(post_semantic_search(app_module_second.app, "spicy", 2))
        assert second_results.status_code in range(200, 300)
        second_payload = second_results.json()
        assert [item["recipeId"] for item in second_payload] == [str(recipe_ids[0]), str(recipe_ids[1])]
    finally:
        if collection_id:
            delete_collection(collection_id)


async def post_semantic_search(app, query: str, top_k: int):
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://testserver") as client:
        return await client.post("/api/semantic-search", json={"query": query, "topK": top_k})


async def get(app, path: str):
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://testserver") as client:
        return await client.get(path)


async def find_existing_recipe_ids(app, count: int, max_id: int = 500):
    results = []
    for candidate_id in range(1, max_id + 1):
        response = await get(app, f"/api/recipes/{candidate_id}")
        if response.status_code != 200:
            continue

        payload = response.json()
        recipe = payload.get("recipe") or {}
        recipe_id = recipe.get("id")
        if isinstance(recipe_id, int) and recipe_id > 0:
            results.append(recipe_id)
            if len(results) >= count:
                break

    return results


def get_collection_id_by_name(collection_name: str):
    path = (
        f"/api/v2/tenants/{parse.quote(TENANT, safe='')}/"
        f"databases/{parse.quote(DATABASE, safe='')}/collections"
    )
    with request.urlopen(f"{CHROMA_BASE_URL}{path}", timeout=10) as response:
        collections = json.loads(response.read().decode("utf-8"))

    for item in collections:
        if item.get("name") == collection_name and item.get("id"):
            return item["id"]

    return None


def upsert_embeddings(collection_id: str, ids: list[str], embeddings: list[list[float]]):
    payload = json.dumps({"ids": ids, "embeddings": embeddings}).encode("utf-8")
    path = (
        f"/api/v2/tenants/{parse.quote(TENANT, safe='')}/"
        f"databases/{parse.quote(DATABASE, safe='')}/collections/{parse.quote(collection_id, safe='')}/upsert"
    )
    req = request.Request(
        f"{CHROMA_BASE_URL}{path}",
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with request.urlopen(req, timeout=10):
        pass


def delete_collection(collection_id: str):
    delete_by_id_path = (
        f"/api/v2/tenants/{parse.quote(TENANT, safe='')}/"
        f"databases/{parse.quote(DATABASE, safe='')}/collections/by-id/{parse.quote(collection_id, safe='')}"
    )
    try:
        delete_request = request.Request(f"{CHROMA_BASE_URL}{delete_by_id_path}", method="DELETE")
        with request.urlopen(delete_request, timeout=10):
            return
    except Exception:
        pass

    delete_path = (
        f"/api/v2/tenants/{parse.quote(TENANT, safe='')}/"
        f"databases/{parse.quote(DATABASE, safe='')}/collections/{parse.quote(collection_id, safe='')}/delete"
    )
    fallback = request.Request(
        f"{CHROMA_BASE_URL}{delete_path}",
        data=json.dumps({"ids": []}).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with request.urlopen(fallback, timeout=10):
            pass
    except Exception:
        pass
