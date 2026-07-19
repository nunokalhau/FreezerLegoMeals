#!/usr/bin/env python3
"""Integration tests for Python Assistant RAG orchestration through the existing endpoint."""

import asyncio
import importlib.util
from pathlib import Path
from types import SimpleNamespace

import httpx


SRC_ROOT = Path(__file__).resolve().parents[3]
APP_PATH = SRC_ROOT / "api" / "WebApi.Python" / "app.py"


def load_app_module():
    app_spec = importlib.util.spec_from_file_location("webapi_python_app_assistant_rag_integration", APP_PATH)
    if app_spec is None or app_spec.loader is None:
        raise ImportError(f"Unable to load app module from {APP_PATH}")

    app_module = importlib.util.module_from_spec(app_spec)
    app_spec.loader.exec_module(app_module)
    return app_module


def test_assistant_chat_uses_rag_and_keeps_public_contract():
    app_module = load_app_module()
    agent = app_module.MealPlanningAgent(StubOllamaClient(), StubToolExecutor(), StubRetrievalService(), StubPromptBuilder())
    app_module.assistant_service = app_module.AssistantService(app_module.InMemoryConversationStore(), app_module.Orchestrator([agent]))

    response = asyncio.run(post_assistant_chat(app_module.app))

    assert response.status_code in range(200, 300)
    payload = response.json()
    assert sorted(payload.keys()) == ["conversationId", "response"]
    assert payload["conversationId"]
    assert "Use the spicy chicken recipe." in payload["response"]
    assert "Sources:" in payload["response"]
    assert "1: Spicy Chicken" in payload["response"]


async def post_assistant_chat(app):
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://testserver") as client:
        return await client.post("/api/assistant/chat", json={"message": "What spicy chicken meal can I cook?"})


class StubOllamaClient:
    def __init__(self):
        self.calls = 0

    def chat(self, model, messages, tools=None):
        self.calls += 1
        if self.calls == 1:
            return SimpleNamespace(content="direct draft", tool_calls=[])
        return SimpleNamespace(content="Use the spicy chicken recipe.", tool_calls=[])


class StubToolExecutor:
    def get_tools(self):
        return []

    def execute(self, name, arguments):
        raise AssertionError("Tool execution was not expected")


class StubRetrievalService:
    def retrieve(self, question):
        source = SimpleNamespace(recipeId="1", title="Spicy Chicken", similarityScore=0.91)
        recipe = SimpleNamespace(recipeId="1", title="Spicy Chicken")
        return SimpleNamespace(question=question, recipes=[recipe], sources=[source])


class StubPromptBuilder:
    def build(self, question, recipes):
        return "rag prompt"
