#!/usr/bin/env python3
"""End-to-end integration tests for the Python AI Assistant endpoint."""

import importlib.util
import asyncio
import json
from pathlib import Path
from urllib import request
from urllib.error import URLError

import httpx
import pytest


OLLAMA_BASE_URL = "http://localhost:11434"
OLLAMA_REQUIRED_MESSAGE = (
    "Local Ollama instance is required at http://localhost:11434 with at least one available model."
)

SRC_ROOT = Path(__file__).resolve().parents[3]
APP_PATH = SRC_ROOT / "api" / "WebApi.Python" / "app.py"


def get_ollama_model_or_skip() -> str:
    tags_request = request.Request(f"{OLLAMA_BASE_URL}/api/tags", method="GET")

    try:
        with request.urlopen(tags_request, timeout=2) as response:
            if response.status < 200 or response.status >= 300:
                pytest.skip(f"{OLLAMA_REQUIRED_MESSAGE} GET /api/tags returned {response.status}.")

            tags = json.loads(response.read().decode("utf-8"))
    except TimeoutError as error:
        pytest.skip(f"{OLLAMA_REQUIRED_MESSAGE} GET /api/tags timed out: {error}")
    except URLError as error:
        pytest.skip(f"{OLLAMA_REQUIRED_MESSAGE} GET /api/tags failed: {error}")

    models = tags.get("models") or []
    model = next(
        (
            candidate.get("name")
            for candidate in models
            if "completion" in (candidate.get("capabilities") or [])
        ),
        None,
    )
    if not model:
        pytest.skip(f"{OLLAMA_REQUIRED_MESSAGE} GET /api/tags returned no completion-capable models.")

    return model


def load_app_module():
    app_spec = importlib.util.spec_from_file_location("webapi_python_app_integration", APP_PATH)
    if app_spec is None or app_spec.loader is None:
        raise ImportError(f"Unable to load app module from {APP_PATH}")

    app_module = importlib.util.module_from_spec(app_spec)
    app_spec.loader.exec_module(app_module)
    return app_module


def test_assistant_chat_returns_non_empty_message_from_local_ollama(monkeypatch):
    model = get_ollama_model_or_skip()
    monkeypatch.setenv("OLLAMA_BASE_URL", OLLAMA_BASE_URL)
    monkeypatch.setenv("OLLAMA_DEFAULT_MODEL", model)
    monkeypatch.setenv("OLLAMA_TIMEOUT", "60")

    app_module = load_app_module()
    response = asyncio.run(post_assistant_chat(app_module.app))

    assert response.status_code in range(200, 300)
    payload = response.json()
    assert isinstance(payload, dict)
    assert isinstance(payload.get("response"), str)
    assert payload["response"].strip()


async def post_assistant_chat(app):
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://testserver") as client:
        return await client.post(
            "/api/assistant/chat",
            json={"message": "Reply with the single word: OK"},
        )