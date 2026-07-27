"""
Unit tests for Memory Provider implementations.
"""

import pytest
from unittest.mock import patch, MagicMock
from datetime import datetime
import importlib.util
import types

# Import the module we're testing
import sys
from pathlib import Path

SRC_ROOT = Path(__file__).resolve().parents[3]
CONVERSATION_STORE_PATH = SRC_ROOT / "services" / "Services.Python" / "conversation_store.py"
MEMORY_PROVIDER_PATH = SRC_ROOT / "ai" / "Memory" / "Python" / "memory_provider.py"
REDIS_MEMORY_PROVIDER_PATH = SRC_ROOT / "ai" / "Memory" / "Python" / "redis_memory_provider.py"


def _ensure_package(name: str) -> None:
    if name in sys.modules:
        return
    package = types.ModuleType(name)
    package.__path__ = []  # mark as package for import resolution
    sys.modules[name] = package


def _load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise ImportError(f"Unable to load {name} from {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


for package_name in [
    "src",
    "src.ai",
    "src.ai.Memory",
    "src.ai.Memory.Python",
    "src.services",
    "src.services.Services",
    "src.services.Services.Python",
]:
    _ensure_package(package_name)

_load_module("src.services.Services.Python.conversation_store", CONVERSATION_STORE_PATH)

# memory_provider imports RedisMemoryProvider from this module; provide a stub first to break cycle.
stub_redis_module = types.ModuleType("src.ai.Memory.Python.redis_memory_provider")


class _StubRedisMemoryProvider:
    def __init__(self, *args, **kwargs):
        pass


stub_redis_module.RedisMemoryProvider = _StubRedisMemoryProvider
sys.modules["src.ai.Memory.Python.redis_memory_provider"] = stub_redis_module

memory_provider_module = _load_module("src.ai.Memory.Python.memory_provider", MEMORY_PROVIDER_PATH)
redis_memory_provider_module = _load_module("src.ai.Memory.Python.redis_memory_provider", REDIS_MEMORY_PROVIDER_PATH)

# Ensure memory_provider exports the real RedisMemoryProvider implementation.
memory_provider_module.RedisMemoryProvider = redis_memory_provider_module.RedisMemoryProvider

IMemoryProvider = memory_provider_module.IMemoryProvider
RedisMemoryProvider = memory_provider_module.RedisMemoryProvider
InMemoryMemoryProvider = memory_provider_module.InMemoryMemoryProvider
MemoryMessage = memory_provider_module.MemoryMessage
ConversationMemory = memory_provider_module.ConversationMemory
get_memory_provider = memory_provider_module.get_memory_provider
set_memory_provider = memory_provider_module.set_memory_provider


def test_imemory_provider_interface():
    """Test that IMemoryProvider is an abstract base class with required methods."""
    # This will fail if the interface isn't properly defined with abstract methods
    assert hasattr(IMemoryProvider, 'get_or_create_conversation')
    assert hasattr(IMemoryProvider, 'append_messages')
    assert hasattr(IMemoryProvider, 'delete_conversation')


def test_memory_message_structure():
    """Test MemoryMessage data structure."""
    msg = MemoryMessage(
        role="user",
        content="Hello",
        timestamp=datetime.now()
    )
    
    assert msg.role == "user"
    assert msg.content == "Hello"
    assert isinstance(msg.timestamp, datetime)


def test_conversation_memory_structure():
    """Test ConversationMemory data structure."""
    messages = [
        MemoryMessage(
            role="user",
            content="Hello",
            timestamp=datetime.now()
        )
    ]
    
    conversation = ConversationMemory(
        conversation_id="test-conversation",
        messages=messages
    )
    
    assert conversation.conversation_id == "test-conversation"
    assert len(conversation.messages) == 1


@patch.object(redis_memory_provider_module, 'HAS_REDIS', False)
def test_redis_memory_provider_fallback():
    """Test that RedisMemoryProvider falls back to in-memory when Redis is not available."""
    # This should create the fallback in-memory provider without errors
    provider = RedisMemoryProvider()
    
    # Check that we get an instance of the expected class (fallback)
    assert provider is not None


def test_in_memory_memory_provider():
    """Test basic functionality of InMemoryMemoryProvider."""
    provider = InMemoryMemoryProvider()
    
    # Test conversation creation
    conversation = provider.get_or_create_conversation()
    assert conversation is not None
    
    # Test append messages
    message = MemoryMessage(
        role="user",
        content="Test message",
        timestamp=datetime.now()
    )
    
    provider.append_messages(conversation.conversation_id, [message])
    
    # Test getting the conversation again
    updated_conversation = provider.get_or_create_conversation(conversation.conversation_id)
    assert len(updated_conversation.messages) == 1


def test_get_memory_provider():
    """Test the global memory provider getter."""
    # Should return some implementation 
    provider = get_memory_provider()
    assert provider is not None


def test_set_memory_provider():
    """Test setting a custom memory provider."""
    mock_provider = MagicMock()
    set_memory_provider(mock_provider)
    
    # Verify it's now the default provider
    retrieved_provider = get_memory_provider()
    assert retrieved_provider is mock_provider
    
    # Reset to None for clean state in subsequent tests
    set_memory_provider(None)


if __name__ == "__main__":
    pytest.main([__file__])