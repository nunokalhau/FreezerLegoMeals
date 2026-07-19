#!/usr/bin/env python3
"""Integration tests for the Python semantic search endpoint."""

import asyncio
import importlib.util
from pathlib import Path
from types import SimpleNamespace

import httpx


SRC_ROOT = Path(__file__).resolve().parents[3]
APP_PATH = SRC_ROOT / "api" / "WebApi.Python" / "app.py"


def load_app_module():
    app_spec = importlib.util.spec_from_file_location("webapi_python_app_semantic_integration", APP_PATH)
    if app_spec is None or app_spec.loader is None:
        raise ImportError(f"Unable to load app module from {APP_PATH}")

    app_module = importlib.util.module_from_spec(app_spec)
    app_spec.loader.exec_module(app_module)
    return app_module


def test_semantic_search_endpoint_returns_ranked_matches():
    app_module = load_app_module()
    app_module.semantic_search_service = StubSemanticSearchService()

    response = asyncio.run(post_semantic_search(app_module.app))

    assert response.status_code in range(200, 300)
    payload = response.json()
    assert payload == [
        {
            "recipeId": "1",
            "title": "Spicy Chicken",
            "score": 1.0,
            "matchedText": "spicy chicken dinner",
            "reason": "High semantic similarity between the query and Spicy Chicken.",
        }
    ]


async def post_semantic_search(app):
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://testserver") as client:
        return await client.post("/api/semantic-search", json={"query": "spicy chicken", "topK": 1})


class StubSemanticSearchService:
    def search(self, query: str, top_k: int):
        assert query == "spicy chicken"
        assert top_k == 1
        return [
            SimpleNamespace(
                recipeId="1",
                title="Spicy Chicken",
                score=1.0,
                matchedText="spicy chicken dinner",
                reason="High semantic similarity between the query and Spicy Chicken.",
            )
        ]