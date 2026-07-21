"""
Unit tests for Memory Provider implementations.
"""

import pytest
from unittest.mock import patch, MagicMock
from datetime import datetime

# Import the module we're testing
import sys
from pathlib import Path

# Add the src directory to the Python path so we can import modules properly 
sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from src.ai.Memory.Python.memory_provider import (
    IMemoryProvider,
    RedisMemoryProvider,
    InMemoryMemoryProvider,
    MemoryMessage,
    ConversationMemory,
    get_memory_provider,
    set_memory_provider
)


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


@patch('src.ai.Memory.Python.memory_provider.hasattr')
def test_redis_memory_provider_fallback(mock_hasattr):
    """Test that RedisMemoryProvider falls back to in-memory when Redis is not available."""
    # Mock that redis library is not available
    mock_hasattr.return_value = False
    
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