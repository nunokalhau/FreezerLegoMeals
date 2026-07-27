#!/usr/bin/env python3
"""End-to-end integration tests for Python embedding API endpoints."""

import asyncio
import importlib.util
import json
from pathlib import Path
from urllib import request
from urllib.error import URLError

import httpx
import pytest


SRC_ROOT = Path(__file__).resolve().parents[3]
APP_PATH = SRC_ROOT / "api" / "WebApi.Python" / "app.py"
OLLAMA_BASE_URL = "http://localhost:11434"
OLLAMA_EMBEDDING_MODEL = "nomic-embed-text"


def ollama_has_embedding_model_or_skip() -> None:
    try:
        with request.urlopen(f"{OLLAMA_BASE_URL}/api/tags", timeout=2) as response:
            tags = json.loads(response.read().decode("utf-8"))
    except (TimeoutError, URLError) as error:
        pytest.skip(f"Local Ollama is unavailable: {error}")

    names = {model.get("name", "").split(":")[0] for model in tags.get("models", [])}
    if OLLAMA_EMBEDDING_MODEL not in names:
        pytest.skip(f"Ollama model {OLLAMA_EMBEDDING_MODEL} is not installed")


def load_app_module():
    app_spec = importlib.util.spec_from_file_location("webapi_python_embedding_app_integration", APP_PATH)
    if app_spec is None or app_spec.loader is None:
        raise ImportError(f"Unable to load app module from {APP_PATH}")

    app_module = importlib.util.module_from_spec(app_spec)
    app_spec.loader.exec_module(app_module)
    return app_module


async def post_embedding(app, route: str, text: str):
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://testserver") as client:
        return await client.post(route, json={"text": text})


def test_generate_embedding_from_public_api_when_ollama_available(monkeypatch):
    ollama_has_embedding_model_or_skip()
    monkeypatch.setenv("OLLAMA_BASE_URL", OLLAMA_BASE_URL)
    monkeypatch.setenv("OLLAMA_EMBEDDING_MODEL", OLLAMA_EMBEDDING_MODEL)
    monkeypatch.setenv("OLLAMA_EMBEDDING_TIMEOUT_MS", "60000")

    app_module = load_app_module()
    response = asyncio.run(post_embedding(app_module.app, "/api/embeddings", "Chicken curry with rice"))

    assert response.status_code in range(200, 300)
    payload = response.json()
    assert payload["model"] == OLLAMA_EMBEDDING_MODEL
    assert payload["dimensions"] > 0
    assert len(payload["embedding"]) == payload["dimensions"]


def test_generate_embedding_rejects_blank_text():
    app_module = load_app_module()

    response = asyncio.run(post_embedding(app_module.app, "/api/embeddings", " "))

    assert response.status_code == 400
