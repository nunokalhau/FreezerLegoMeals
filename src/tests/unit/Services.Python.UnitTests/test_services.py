#!/usr/bin/env python3
"""
Unit tests for MealService and ShoppingService implementations.
These are proper pytest-based unit tests with mocked repository dependencies.
"""

import sys
import os
import time
import json
import unittest.mock as mock
from pathlib import Path
import importlib.util

SRC_ROOT = Path(__file__).resolve().parents[3]
ASSISTANT_SERVICE_PATH = SRC_ROOT / 'services' / 'Services.Python' / 'assistant_service.py'
ORCHESTRATION_PATH = SRC_ROOT / 'orchestration' / 'Python'
MEAL_SERVICE_PATH = SRC_ROOT / 'services' / 'Services.Python' / 'meal_service.py'
OLLAMA_CLIENT_PATH = SRC_ROOT / 'services' / 'Services.Python' / 'ollama_client.py'
SHOPPING_SERVICE_PATH = SRC_ROOT / 'services' / 'Services.Python' / 'shopping_service.py'

if str(ORCHESTRATION_PATH) not in sys.path:
    sys.path.insert(0, str(ORCHESTRATION_PATH))

assistant_spec = importlib.util.spec_from_file_location('assistant_service', ASSISTANT_SERVICE_PATH)
assistant_orchestrator_spec = importlib.util.spec_from_file_location('orchestration_python_assistant_orchestrator_service_tests', ORCHESTRATION_PATH / 'assistant_orchestrator.py')
meal_planning_agent_spec = importlib.util.spec_from_file_location('orchestration_python_meal_planning_agent_service_tests', ORCHESTRATION_PATH / 'meal_planning_agent.py')
meal_spec = importlib.util.spec_from_file_location('meal_service', MEAL_SERVICE_PATH)
ollama_spec = importlib.util.spec_from_file_location('ollama_client', OLLAMA_CLIENT_PATH)
shopping_spec = importlib.util.spec_from_file_location('shopping_service', SHOPPING_SERVICE_PATH)

if assistant_spec is None or assistant_spec.loader is None:
    raise ImportError(f'Unable to load assistant_service from {ASSISTANT_SERVICE_PATH}')
if assistant_orchestrator_spec is None or assistant_orchestrator_spec.loader is None:
    raise ImportError(f'Unable to load assistant_orchestrator from {ORCHESTRATION_PATH}')
if meal_planning_agent_spec is None or meal_planning_agent_spec.loader is None:
    raise ImportError(f'Unable to load meal_planning_agent from {ORCHESTRATION_PATH}')
if meal_spec is None or meal_spec.loader is None:
    raise ImportError(f'Unable to load meal_service from {MEAL_SERVICE_PATH}')
if ollama_spec is None or ollama_spec.loader is None:
    raise ImportError(f'Unable to load ollama_client from {OLLAMA_CLIENT_PATH}')
if shopping_spec is None or shopping_spec.loader is None:
    raise ImportError(f'Unable to load shopping_service from {SHOPPING_SERVICE_PATH}')

assistant_module = importlib.util.module_from_spec(assistant_spec)
assistant_spec.loader.exec_module(assistant_module)
assistant_orchestrator_module = importlib.util.module_from_spec(assistant_orchestrator_spec)
assistant_orchestrator_spec.loader.exec_module(assistant_orchestrator_module)
meal_planning_agent_module = importlib.util.module_from_spec(meal_planning_agent_spec)
meal_planning_agent_spec.loader.exec_module(meal_planning_agent_module)
meal_module = importlib.util.module_from_spec(meal_spec)
meal_spec.loader.exec_module(meal_module)
ollama_module = importlib.util.module_from_spec(ollama_spec)
sys.modules['ollama_client'] = ollama_module
ollama_spec.loader.exec_module(ollama_module)
shopping_module = importlib.util.module_from_spec(shopping_spec)
shopping_spec.loader.exec_module(shopping_module)

# Register modules so unittest.mock.patch can resolve by module name.
sys.modules['meal_service'] = meal_module
sys.modules['shopping_service'] = shopping_module

import pytest
AssistantService = assistant_module.AssistantService
AssistantOptions = assistant_module.AssistantOptions
MealService = meal_module.MealService
OllamaClient = ollama_module.OllamaClient
OllamaClientConfig = ollama_module.OllamaClientConfig
ShoppingService = shopping_module.ShoppingService


class TestAssistantService:
    """Unit tests for AssistantService."""

    def test_chat_delegates_to_ollama_client(self):
        ollama_client = mock.Mock()
        ollama_client.chat.return_value = FakeOllamaResult('assistant response')
        conversation_store = FakeConversationStore()
        tool_executor = FakeToolExecutor()
        service = create_assistant_service(ollama_client, conversation_store, tool_executor, AssistantOptions(system_prompt='system prompt'))

        result = service.chat('Hello')

        assert result.response == 'assistant response'
        assert result.conversation_id == 'conversation-1'
        messages = ollama_client.chat.call_args.args[1]
        assert [message.role for message in messages] == ['System', 'User']
        assert [message.content for message in messages] == ['system prompt', 'Hello']
        assert [message.content for message in conversation_store.messages] == ['Hello', 'assistant response']

    def test_initialization_requires_orchestrator(self):
        with pytest.raises(ValueError):
            AssistantService(mock.Mock(), None)

    def test_initialization_requires_conversation_store(self):
        with pytest.raises(ValueError):
            AssistantService(None, mock.Mock())

    def test_chat_executes_one_tool_call_and_returns_final_response(self):
        ollama_client = mock.Mock()
        ollama_client.chat.side_effect = [
            FakeOllamaResult('', [{'name': 'example_tool', 'arguments': {'message': 'hello'}}]),
            FakeOllamaResult('done'),
        ]
        tool_executor = FakeToolExecutor()
        conversation_store = FakeConversationStore()
        service = create_assistant_service(ollama_client, conversation_store, tool_executor, AssistantOptions(system_prompt='system prompt'))

        result = service.chat('Use tool')

        assert result.response == 'done'
        assert tool_executor.calls == [('example_tool', {'message': 'hello'})]
        assert any(message.role == 'Tool' for message in conversation_store.messages)

    def test_chat_executes_multiple_sequential_tool_calls(self):
        ollama_client = mock.Mock()
        ollama_client.chat.side_effect = [
            FakeOllamaResult('', [{'name': 'example_tool', 'arguments': {}}]),
            FakeOllamaResult('', [{'name': 'second_tool', 'arguments': {}}]),
            FakeOllamaResult('complete'),
        ]
        tool_executor = FakeToolExecutor()
        service = create_assistant_service(ollama_client, FakeConversationStore(), tool_executor, AssistantOptions(system_prompt='system prompt'))

        result = service.chat('Use tools')

        assert result.response == 'complete'
        assert [call[0] for call in tool_executor.calls] == ['example_tool', 'second_tool']

    def test_chat_appends_tool_failure_and_returns_final_response(self):
        ollama_client = mock.Mock()
        ollama_client.chat.side_effect = [
            FakeOllamaResult('', [{'name': 'example_tool', 'arguments': {}}]),
            FakeOllamaResult('tool failed gracefully'),
        ]
        tool_executor = FakeToolExecutor(success=False)
        conversation_store = FakeConversationStore()
        service = create_assistant_service(ollama_client, conversation_store, tool_executor, AssistantOptions(system_prompt='system prompt'))

        result = service.chat('Use failing tool')

        assert result.response == 'tool failed gracefully'
        assert 'failed' in next(message.content for message in conversation_store.messages if message.role == 'Tool')

    def test_chat_handles_invalid_tool(self):
        ollama_client = mock.Mock()
        ollama_client.chat.side_effect = [
            FakeOllamaResult('', [{'name': 'missing_tool', 'arguments': {}}]),
            FakeOllamaResult('invalid tool handled'),
        ]
        tool_executor = FakeToolExecutor(raise_on_execute=True)
        service = create_assistant_service(ollama_client, FakeConversationStore(), tool_executor, AssistantOptions(system_prompt='system prompt'))

        result = service.chat('Use missing tool')

        assert result.response == 'invalid tool handled'

    def test_chat_returns_graceful_error_when_tool_limit_exceeded(self):
        ollama_client = mock.Mock()
        ollama_client.chat.return_value = FakeOllamaResult('', [
            {'name': 'example_tool', 'arguments': {}},
            {'name': 'second_tool', 'arguments': {}},
        ])
        tool_executor = FakeToolExecutor()
        service = create_assistant_service(
            ollama_client,
            FakeConversationStore(),
            tool_executor,
            AssistantOptions(system_prompt='system prompt', maximum_tool_calls_per_request=1),
        )

        result = service.chat('Loop')

        assert 'maximum tool call limit' in result.response
        assert len(tool_executor.calls) == 1


def create_assistant_service(ollama_client, conversation_store, tool_executor, options=None):
    agent = meal_planning_agent_module.MealPlanningAgent(ollama_client, tool_executor)
    orchestrator = assistant_orchestrator_module.AssistantOrchestrator([agent])
    return AssistantService(conversation_store, orchestrator, options)


class TestOllamaClient:
    """Unit tests for OllamaClient."""

    def test_chat_uses_default_model_and_returns_assistant_content(self, monkeypatch):
        captured = {}

        class FakeResponse:
            def __enter__(self):
                return self

            def __exit__(self, exc_type, exc, traceback):
                return False

            def read(self):
                return b'{"message":{"role":"assistant","content":"Hello from Ollama"}}'

        def fake_urlopen(chat_request, timeout):
            captured['url'] = chat_request.full_url
            captured['body'] = chat_request.data
            captured['timeout'] = timeout
            return FakeResponse()

        monkeypatch.setattr(ollama_module.request, 'urlopen', fake_urlopen)
        client = OllamaClient(OllamaClientConfig(
            base_url='http://localhost:11434',
            default_model='llama3.2',
            timeout=30.0,
        ))

        result = client.chat(None, [
            {'role': 'System', 'content': 'system prompt'},
            {'role': 'User', 'content': 'Hello'},
        ])

        assert result.content == 'Hello from Ollama'
        assert result.tool_calls == []
        assert captured['url'] == 'http://localhost:11434/api/chat'
        assert captured['timeout'] == 30.0
        body = json.loads(captured['body'].decode('utf-8'))
        assert body == {
            'model': 'llama3.2',
            'messages': [
                {
                    'role': 'system',
                    'content': 'system prompt',
                },
                {
                    'role': 'user',
                    'content': 'Hello',
                }
            ],
            'tools': [],
            'stream': False,
        }

    def test_chat_uses_provided_model(self, monkeypatch):
        captured = {}

        class FakeResponse:
            def __enter__(self):
                return self

            def __exit__(self, exc_type, exc, traceback):
                return False

            def read(self):
                return b'{"message":{"content":"ok"}}'

        def fake_urlopen(chat_request, timeout):
            captured['body'] = chat_request.data
            return FakeResponse()

        monkeypatch.setattr(ollama_module.request, 'urlopen', fake_urlopen)
        client = OllamaClient(OllamaClientConfig(default_model='default-model'))

        client.chat('custom-model', [{'role': 'User', 'content': 'Hello'}])

        body = json.loads(captured['body'].decode('utf-8'))
        assert body['model'] == 'custom-model'

    def test_chat_rejects_empty_user_message(self):
        client = OllamaClient(OllamaClientConfig())

        with pytest.raises(ValueError):
            client.chat('llama3.2', [])

    def test_chat_returns_native_tool_calls(self, monkeypatch):
        class FakeResponse:
            def __enter__(self):
                return self

            def __exit__(self, exc_type, exc, traceback):
                return False

            def read(self):
                return b'{"message":{"content":"","tool_calls":[{"function":{"name":"example_tool","arguments":{"message":"hello"}}}]}}'

        monkeypatch.setattr(ollama_module.request, 'urlopen', lambda _request, timeout=None: FakeResponse())
        client = OllamaClient(OllamaClientConfig(default_model='default-model'))

        result = client.chat(None, [{'role': 'User', 'content': 'Hello'}])

        assert result.tool_calls == [{'name': 'example_tool', 'arguments': {'message': 'hello'}}]

    def test_chat_retries_without_tools_when_ollama_rejects_tool_payload(self, monkeypatch):
        captured_bodies = []

        class FakeResponse:
            def __enter__(self):
                return self

            def __exit__(self, exc_type, exc, traceback):
                return False

            def read(self):
                return b'{"message":{"content":"fallback ok"}}'

        def fake_urlopen(chat_request, timeout=None):
            captured_bodies.append(json.loads(chat_request.data.decode('utf-8')))
            if len(captured_bodies) == 1:
                raise ollama_module.HTTPError(chat_request.full_url, 400, 'Bad Request', hdrs=None, fp=None)
            return FakeResponse()

        monkeypatch.setattr(ollama_module.request, 'urlopen', fake_urlopen)
        client = OllamaClient(OllamaClientConfig(default_model='default-model'))

        result = client.chat(None, [{'role': 'User', 'content': 'Hello'}], [{'name': 'search_recipes', 'parameters': ['ingredients']}])

        assert result.content == 'fallback ok'
        assert captured_bodies[0]['tools']
        assert captured_bodies[1]['tools'] == []


class TestMealService:
    """Unit tests for MealService."""
    
    def test_initialization(self):
        """Test that MealService can be initialized successfully."""
        service = MealService()
        assert service is not None
        assert hasattr(service, 'repository')
    
    def test_search_recipes_by_ingredients(self):
        """Test search_recipes_by_ingredients method."""
        mock_repo_instance = mock.Mock()
        
        expected_data = [
            {"id": 1, "name": "Chicken Curry", "source_path": "/recipes/chicken_curry.md", "matched_ingredients": ["chicken"]}
        ]
        mock_repo_instance.search_recipes_by_ingredients.return_value = expected_data
        
        service = MealService()
        service.repository = mock_repo_instance
        result = service.search_recipes_by_ingredients(["chicken"])
        
        assert result == expected_data
        mock_repo_instance.search_recipes_by_ingredients.assert_called_once_with(["chicken"])
    
    def test_get_recipe_by_id(self):
        """Test get_recipe_by_id method."""
        mock_repo_instance = mock.Mock()
        
        expected_data = {
            "id": 1,
            "name": "Chicken Curry",
            "source_path": "/recipes/chicken_curry.md"
        }
        mock_repo_instance.get_recipe_by_id.return_value = expected_data
        
        service = MealService()
        service.repository = mock_repo_instance
        result = service.get_recipe_by_id(1)
        
        assert result == expected_data
        mock_repo_instance.get_recipe_by_id.assert_called_once_with(1)
    
    def test_find_meals_with_ingredients_success(self):
        """Test find_meals_with_ingredients with ingredients found."""
        mock_repo_instance = mock.Mock()
        
        expected_data = [
            {"id": 1, "name": "Chicken Curry", "source_path": "/recipes/chicken_curry.md", "matched_ingredients": ["chicken"]}
        ]
        mock_repo_instance.search_recipes_by_ingredients.return_value = expected_data
        
        service = MealService()
        service.repository = mock_repo_instance
        result = service.find_meals_with_ingredients("Find meals with chicken")
        
        assert "query" in result
        assert "search_terms" in result
        assert "total_recipes_found" in result
        assert result["total_recipes_found"] == 1
        
    def test_find_meals_with_ingredients_no_ingredients(self):
        """Test find_meals_with_ingredients when no ingredients found."""
        mock_repo_instance = mock.Mock()
        
        mock_repo_instance.search_recipes_by_ingredients.return_value = []
        
        service = MealService()
        service.repository = mock_repo_instance
        result = service.find_meals_with_ingredients("Find meals with nonexistent ingredient")
        
        assert result["total_recipes_found"] == 0
        assert "message" in result
    
    def test_get_recipe_details_success(self):
        """Test get_recipe_details with valid recipe."""
        mock_repo_instance = mock.Mock()
        
        expected_data = {
            "id": 1,
            "name": "Chicken Curry",
            "source_path": "/recipes/chicken_curry.md"
        }
        mock_repo_instance.get_recipe_by_id.return_value = expected_data
        
        service = MealService()
        service.repository = mock_repo_instance
        result = service.get_recipe_details(1)
        
        assert result["recipe"] == expected_data
        assert "message" in result

    def test_get_recipe_details_not_found(self):
        """Test get_recipe_details with invalid recipe."""
        mock_repo_instance = mock.Mock()
        
        mock_repo_instance.get_recipe_by_id.return_value = None
        
        service = MealService()
        service.repository = mock_repo_instance
        result = service.get_recipe_details(999)
        
        assert "error" in result


class FakeConversation:
    def __init__(self, conversation_id, messages):
        self.conversation_id = conversation_id
        self.messages = messages


class FakeConversationStore:
    def __init__(self):
        self.messages = []

    def get_or_create_conversation(self, conversation_id=None):
        return FakeConversation(conversation_id or 'conversation-1', list(self.messages))

    def append_messages(self, conversation_id, messages):
        self.messages.extend(messages)


class FakeOllamaResult:
    def __init__(self, content, tool_calls=None):
        self.content = content
        self.tool_calls = tool_calls or []


class FakeToolExecutor:
    def __init__(self, success=True, raise_on_execute=False):
        self.success = success
        self.raise_on_execute = raise_on_execute
        self.calls = []

    def get_tools(self):
        return [
            {'name': 'example_tool', 'description': 'Example tool'},
            {'name': 'second_tool', 'description': 'Second tool'},
        ]

    def execute(self, tool_name, parameters=None):
        if self.raise_on_execute:
            raise ValueError(f'Unknown tool: {tool_name}')

        self.calls.append((tool_name, parameters or {}))
        return {
            'success': self.success,
            'tool': tool_name,
            'output': {'ok': True} if self.success else None,
            'error': None if self.success else 'failed',
        }


class TestShoppingService:
    """Unit tests for ShoppingService."""
    
    def test_initialization(self):
        """Test that ShoppingService can be initialized successfully."""
        service = ShoppingService()
        assert service is not None
        assert hasattr(service, 'repository')
    
    def test_get_recipe_ingredients_success(self):
        """Test get_recipe_ingredients method with valid recipe."""
        mock_repo_instance = mock.Mock()
        
        expected_data = [
            {"name": "chicken breast", "amount": 500.0, "unit": "g"}
        ]
        mock_repo_instance.get_recipe_ingredients.return_value = expected_data
        mock_repo_instance.get_recipe_details.return_value = [
            {"id": 1, "name": "Chicken Curry"}
        ]
        
        service = ShoppingService()
        service.repository = mock_repo_instance
        result = service.get_recipe_ingredients("Chicken Curry")
        
        assert result == expected_data
        mock_repo_instance.get_recipe_ingredients.assert_called_once_with(1)
    
    def test_get_multiple_recipe_ingredients(self):
        """Test get_multiple_recipe_ingredients method."""
        mock_repo_instance = mock.Mock()
        
        expected_data = {
            "Chicken Curry": [{"name": "chicken breast", "amount": 500.0, "unit": "g"}]
        }
        mock_repo_instance.get_recipe_ingredients.return_value = expected_data["Chicken Curry"]
        mock_repo_instance.get_recipe_details.return_value = [
            {"id": 1, "name": "Chicken Curry"}
        ]
        
        service = ShoppingService()
        service.repository = mock_repo_instance
        result = service.get_multiple_recipe_ingredients(["Chicken Curry"])
        
        assert "Chicken Curry" in result
        assert len(result["Chicken Curry"]) == 1
    
    def test_generate_shopping_list_basic(self):
        """Test generate_shopping_list with basic functionality."""
        mock_repo_instance = mock.Mock()
        
        mock_repo_instance.get_recipe_ingredients.side_effect = [
            [{"name": "chicken breast", "amount": 500.0, "unit": "g"}],
            [{"name": "onion", "amount": 2.0, "unit": "pcs"}]
        ]
        mock_repo_instance.get_recipe_details.side_effect = [
            [{"id": 1, "name": "Chicken Curry"}],
            [{"id": 2, "name": "Vegetable Soup"}],
        ]
        
        service = ShoppingService()
        service.repository = mock_repo_instance
        result = service.generate_shopping_list(["Chicken Curry", "Vegetable Soup"], scale_factor=1.0)
        
        assert "recipes" in result
        assert "total_recipes" in result
        assert "ingredients" in result
        assert result["total_recipes"] == 2
    
    def test_generate_shopping_list_empty(self):
        """Test generate_shopping_list with empty recipe list."""
        mock_repo_instance = mock.Mock()
        
        service = ShoppingService()
        service.repository = mock_repo_instance
        result = service.generate_shopping_list([], scale_factor=1.0)
        
        assert "recipes" in result
        assert result["total_recipes"] == 0
        assert "ingredients" in result
    
    def test_generate_shopping_list_with_scaling(self):
        """Test generate_shopping_list with scale factor."""
        mock_repo_instance = mock.Mock()
        
        mock_repo_instance.get_recipe_ingredients.return_value = [
            {"name": "chicken breast", "amount": 500.0, "unit": "g"}
        ]
        mock_repo_instance.get_recipe_details.return_value = [
            {"id": 1, "name": "Chicken Curry"}
        ]
        
        service = ShoppingService()
        service.repository = mock_repo_instance
        result = service.generate_shopping_list(["Chicken Curry"], scale_factor=2.0)
        
        assert result["scale_factor"] == 2.0

    def test_get_recipe_info_success(self):
        """Test get_recipe_info returns mapped recipe info."""
        mock_repo_instance = mock.Mock()
        mock_repo_instance.get_recipe_details.return_value = [
            {
                "id": 1,
                "name": "Chicken Curry",
                "servings": 2,
                "time_to_prepare": 30,
            }
        ]

        service = ShoppingService()
        service.repository = mock_repo_instance

        info = service.get_recipe_info("Chicken Curry")

        assert info is not None
        assert info["id"] == 1
        assert info["name"] == "Chicken Curry"

    def test_get_recipe_info_not_found(self):
        """Test get_recipe_info returns None when recipe is not found."""
        mock_repo_instance = mock.Mock()
        mock_repo_instance.get_recipe_details.return_value = []

        service = ShoppingService()
        service.repository = mock_repo_instance

        info = service.get_recipe_info("Missing Recipe")

        assert info is None

    def test_get_recipe_ingredients_returns_empty_for_blank_identifier(self):
        """Test get_recipe_ingredients returns empty for blank identifiers."""
        service = ShoppingService()
        service.repository = mock.Mock()

        assert service.get_recipe_ingredients("   ") == []

    def test_get_recipe_info_returns_none_for_blank_identifier(self):
        """Test get_recipe_info returns None for blank identifiers."""
        service = ShoppingService()
        service.repository = mock.Mock()

        assert service.get_recipe_info("   ") is None
        
    def test_categorize_ingredients(self):
        """Test _categorize_ingredients helper method."""
        service = ShoppingService()
        
        ingredients = [
            {"name": "chicken breast", "amount": 500.0, "unit": "g"},
            {"name": "onion", "amount": 2.0, "unit": "pcs"}
        ]
        
        categorized = service._categorize_ingredients(ingredients)
        
        assert isinstance(categorized, dict)
        assert len(categorized) >= 1


def test_service_direct_instantiation():
    """Test that services can be instantiated without errors (smoke test)."""
    meal_service = MealService()
    shopping_service = ShoppingService()
    
    assert meal_service is not None
    assert shopping_service is not None


if __name__ == "__main__":
    pytest.main([__file__, "-v"])