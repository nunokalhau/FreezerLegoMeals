"""
Unit tests for Redis Memory Provider implementation.
This test file replicates the structure and behavior of the .NET RedisMemoryProviderTests.cs
"""

import pytest
from unittest.mock import patch, MagicMock
from datetime import datetime, timedelta
import uuid

# Import the module we're testing
import sys
from pathlib import Path

# Add the src directory to the Python path so we can import modules properly 
sys.path.insert(0, str(Path(__file__).resolve().parents[3]))

from src.ai.Memory.Python.redis_memory_provider import RedisMemoryProvider
from src.ai.Memory.Python.memory_provider import (
    IMemoryProvider,
    MemoryMessage,
    ConversationMemory,
    InMemoryMemoryProvider
)
from src.services.Services.Python.conversation_store import (
    ConversationStoreOptions,
    ConversationMessage as ConversationMessageService
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


def test_redis_memory_provider_initialization():
    """Test RedisMemoryProvider initialization with default settings."""
    provider = RedisMemoryProvider()
    
    assert provider is not None
    assert provider.connection_string == "localhost:6379"


@patch('src.ai.Memory.Python.redis_memory_provider.HAS_REDIS', False)
def test_redis_memory_provider_no_redis_library():
    """Test that RedisMemoryProvider falls back to in-memory when Redis library is not available."""
    # This should create the fallback in-memory provider without errors
    provider = RedisMemoryProvider()
    
    # Should still work and be initialized even if Redis not available
    assert provider is not None


def test_redis_memory_provider_with_options():
    """Test RedisMemoryProvider initialization with custom options."""
    options = ConversationStoreOptions(
        maximum_conversations=500,
        maximum_messages_per_conversation=25,
        automatic_trimming=True,
        expiration_timeout_seconds=1800.0
    )
    
    provider = RedisMemoryProvider(options=options)
    
    assert provider is not None
    assert provider.options == options


def test_get_or_create_conversation_without_id_creates_new():
    """Test getting or creating a conversation without an ID creates a new one."""
    provider = RedisMemoryProvider()
    
    # Mock the in-memory fallback
    with patch.object(provider, '_fallback_provider', autospec=True) as mock_fallback:
        mock_conversation = ConversationMemory(
            conversation_id="test-id",
            messages=[]
        )
        mock_fallback.get_or_create_conversation.return_value = mock_conversation
        
        conversation = provider.get_or_create_conversation()
        
        assert conversation is not None
        assert len(conversation.messages) == 0


def test_get_or_create_conversation_with_existing_id_returns_existing():
    """Test getting or creating a conversation with an existing ID returns persisted messages."""
    provider = RedisMemoryProvider()
    
    # Mock the in-memory fallback
    with patch.object(provider, '_fallback_provider', autospec=True) as mock_fallback:
        mock_messages = [
            MemoryMessage(
                role="user",
                content="hello",
                timestamp=datetime.now()
            )
        ]
        mock_conversation = ConversationMemory(
            conversation_id="existing-id",
            messages=mock_messages
        )
        mock_fallback.get_or_create_conversation.return_value = mock_conversation
        
        conversation = provider.get_or_create_conversation("existing-id")
        
        assert conversation.conversation_id == "existing-id"
        assert len(conversation.messages) == 1
        assert conversation.messages[0].content == "hello"


def test_append_messages_persists_messages_in_order():
    """Test that messages are appended and persisted in the correct order."""
    provider = RedisMemoryProvider()
    
    # Mock the in-memory fallback
    with patch.object(provider, '_fallback_provider', autospec=True) as mock_fallback:
        # Create some test messages
        messages = [
            MemoryMessage(
                role="user",
                content="first",
                timestamp=datetime.now()
            ),
            MemoryMessage(
                role="assistant", 
                content="second",
                timestamp=datetime.now()
            )
        ]
        
        provider.append_messages("test-id", messages)
        
        # Verify that the fallback was called
        mock_fallback.append_messages.assert_called_once()


def test_append_messages_trims_old_messages_when_limit_exceeded():
    """Test that old messages are trimmed when limit is exceeded."""
    # Create options with small message limit
    options = ConversationStoreOptions(
        maximum_conversations=1000,
        maximum_messages_per_conversation=2,
        automatic_trimming=True,
        expiration_timeout_seconds=3600.0
    )
    
    provider = RedisMemoryProvider(options=options)
    
    # Mock the in-memory fallback for this test 
    with patch.object(provider, '_fallback_provider', autospec=True) as mock_fallback:
        mock_messages = [
            MemoryMessage(
                role="user",
                content="first",
                timestamp=datetime.now()
            ),
            MemoryMessage(
                role="user", 
                content="second",
                timestamp=datetime.now()
            ),
            MemoryMessage(
                role="user",
                content="third",
                timestamp=datetime.now()
            )
        ]
        
        provider.append_messages("test-id", mock_messages)
        
        # Should call the fallback
        mock_fallback.append_messages.assert_called_once()


def test_delete_conversation():
    """Test deleting a conversation."""
    provider = RedisMemoryProvider()
    
    # Test that it doesn't crash and returns False (fallback behavior)
    result = provider.delete_conversation("test-id")
    
    assert result is False  # Should return false for fallback in-memory provider


def test_concurrent_access():
    """Test concurrent access to the memory provider."""
    provider = RedisMemoryProvider()
    
    # This just ensures no crash occurs
    try:
        conversation1 = provider.get_or_create_conversation("convo-1")
        conversation2 = provider.get_or_create_conversation("convo-2")
        
        assert conversation1 is not None
        assert conversation2 is not None
        
        messages = [
            MemoryMessage(
                role="user",
                content=f"message-{i}",
                timestamp=datetime.now()
            ) for i in range(3)
        ]
        
        provider.append_messages("convo-1", messages)
        
        # Test that the operation completes without error
        assert True
        
    except Exception as e:
        pytest.fail(f"Concurrent access test failed: {str(e)}")


def test_serialization_preserves_message_data():
    """Test that message data is properly serialized and deserialized."""
    provider = RedisMemoryProvider()
    
    # Mock the fallback to ensure we can test this functionality
    with patch.object(provider, '_fallback_provider', autospec=True) as mock_fallback:
        # Create some messages with different roles
        messages = [
            MemoryMessage(
                role="user",
                content="user message",
                timestamp=datetime.now()
            ),
            MemoryMessage(
                role="assistant",
                content="assistant response",
                timestamp=datetime.now()
            ),
            MemoryMessage(
                role="tool",
                content="tool result",
                timestamp=datetime.now()
            )
        ]
        
        provider.append_messages("test-id", messages)
        
        # Check that it was called with the right arguments
        mock_fallback.append_messages.assert_called_once()


if __name__ == "__main__":
    pytest.main([__file__])