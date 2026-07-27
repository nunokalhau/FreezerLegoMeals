#!/usr/bin/env python3
"""Unit tests for Python assistant API response contract under RAG-style answers."""

import importlib.util
from pathlib import Path


SRC_ROOT = Path(__file__).resolve().parents[3]
APP_PATH = SRC_ROOT / 'api' / 'WebApi.Python' / 'app.py'

app_spec = importlib.util.spec_from_file_location('webapi_python_app_assistant_contract', APP_PATH)
if app_spec is None or app_spec.loader is None:
    raise ImportError(f'Unable to load app module from {APP_PATH}')

app_module = importlib.util.module_from_spec(app_spec)
app_spec.loader.exec_module(app_module)


def test_assistant_chat_preserves_public_contract_for_rag_style_response(monkeypatch):
    monkeypatch.setattr(
        app_module.assistant_service,
        'chat',
        lambda _message, _conversation_id=None: type(
            'AssistantResult',
            (),
            {
                'conversation_id': 'conversation-1',
                'response': 'Use the spicy chicken recipe.\\n\\nSources:\\n- 1: Spicy Chicken (similarityScore: 0.910000)',
            }
        )()
    )

    response = app_module.chat_with_assistant(
        app_module.AssistantChatRequest(message='What spicy chicken meal can I cook?')
    )

    assert sorted(response.__dict__.keys()) == ['conversationId', 'response']
    assert response.response.startswith('Use the spicy chicken recipe.')
    assert 'Sources:' in response.response
    assert '1: Spicy Chicken' in response.response
