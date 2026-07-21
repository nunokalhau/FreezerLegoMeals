"""
Memory provider implementations for Freezer Lego Meals assistant.
This module provides interfaces and implementations for conversation memory management,
matching the .NET RedisMemoryProvider architecture exactly.
"""

from __future__ import annotations

import abc
import json
import logging
from dataclasses import dataclass, field
from datetime import datetime
from typing import List, Optional

try:
    import redis
    HAS_REDIS = True
except ImportError:
    HAS_REDIS = False

from src.services.Services.Python.conversation_store import (
    ConversationMessage,
    InMemoryConversationStore,
)

logger = logging.getLogger(__name__)


@dataclass(frozen=True)
class MemoryMessage:
    """Represents a single message in the conversation memory."""
    role: str
    content: str
    timestamp: datetime


@dataclass(frozen=True)
class ConversationMemory:
    """Represents a complete conversation history with messages and metadata."""
    conversation_id: str
    messages: List[MemoryMessage]
    
    def to_conversation_messages(self) -> List[ConversationMessage]:
        """Convert MemoryMessage objects to ConversationMessage objects for compatibility."""
        return [
            ConversationMessage(
                role=msg.role,
                content=msg.content,
                timestamp=msg.timestamp
            )
            for msg in self.messages
        ]


class IMemoryProvider(abc.ABC):
    """Interface for memory providers that handle conversation persistence."""

    @abc.abstractmethod
    def get_or_create_conversation(self, conversation_id: Optional[str] = None) -> ConversationMemory:
        """
        Get an existing conversation or create a new one.
        
        Args:
            conversation_id: The ID of the conversation to retrieve or create.
                              If None, a new conversation is created.
                              
        Returns:
            ConversationMemory object containing the conversation history.
        """
        pass

    @abc.abstractmethod
    def append_messages(self, conversation_id: str, messages: List[MemoryMessage]) -> None:
        """
        Append messages to an existing conversation.
        
        Args:
            conversation_id: The ID of the conversation to append to.
            messages: List of MemoryMessage objects to append.
        """
        pass

    @abc.abstractmethod
    def delete_conversation(self, conversation_id: str) -> bool:
        """
        Delete a conversation from memory.
        
        Args:
            conversation_id: The ID of the conversation to delete.
            
        Returns:
            True if the conversation was deleted, False otherwise.
        """
        pass


# Import the RedisMemoryProvider class
from src.ai.Memory.Python.redis_memory_provider import RedisMemoryProvider


class InMemoryMemoryProvider(IMemoryProvider):
    """In-memory implementation of IMemoryProvider as a fallback."""
    
    def __init__(self):
        """Initialize the in-memory memory provider."""
        self._store = InMemoryConversationStore()
        
    def get_or_create_conversation(self, conversation_id: Optional[str] = None) -> ConversationMemory:
        """
        Get an existing conversation or create a new one from in-memory store.
        
        Args:
            conversation_id: The ID of the conversation to retrieve or create.
                              If None, a new conversation is created.
                              
        Returns:
            ConversationMemory object containing the conversation history.
        """
        conversation_history = self._store.get_or_create_conversation(conversation_id)
        return self._to_conversation_memory(conversation_history)

    def append_messages(self, conversation_id: str, messages: List[MemoryMessage]) -> None:
        """
        Append messages to an existing conversation in the in-memory store.
        
        Args:
            conversation_id: The ID of the conversation to append to.
            messages: List of MemoryMessage objects to append.
        """
        conversation_messages = [
            ConversationMessage(
                role=msg.role,
                content=msg.content,
                timestamp=msg.timestamp
            )
            for msg in messages
        ]
        self._store.append_messages(conversation_id, conversation_messages)

    def delete_conversation(self, conversation_id: str) -> bool:
        """
        Delete a conversation from the in-memory store.
        
        Args:
            conversation_id: The ID of the conversation to delete.
            
        Returns:
            Always returns False as in-memory store is not persistent.
        """
        # In-memory stores can't be deleted reliably
        return False

    def _to_conversation_memory(self, conversation_history) -> ConversationMemory:
        """Convert ConversationHistory to ConversationMemory."""
        messages = [
            MemoryMessage(
                role=msg.role,
                content=msg.content,
                timestamp=msg.timestamp
            )
            for msg in conversation_history.messages
        ]
        return ConversationMemory(
            conversation_id=conversation_history.conversation_id,
            messages=messages
        )


# Global default instance for usage convenience
_default_provider: Optional[IMemoryProvider] = None


def get_memory_provider() -> IMemoryProvider:
    """
    Get the default memory provider instance (Redis-based or fallback).
    
    Returns:
        IMemoryProvider instance that can be used for conversation management.
    """
    global _default_provider
    
    if _default_provider is not None:
        return _default_provider
        
    # Try to initialize with Redis first
    try:
        _default_provider = RedisMemoryProvider()
        # Verify we can connect to Redis by performing a simple operation
        if hasattr(_default_provider, '_redis_client') and _default_provider._redis_client is not None:
            _default_provider._redis_client.ping()
        return _default_provider
    except Exception as e:
        logger.warning("Redis connection failed. Using in-memory fallback: %s", str(e))
        # Fallback to in-memory if Redis fails
        _default_provider = InMemoryMemoryProvider()
        return _default_provider


def set_memory_provider(provider: IMemoryProvider) -> None:
    """
    Set the global default memory provider.
    
    Args:
        provider: The IMemoryProvider instance to use.
    """
    global _default_provider
    _default_provider = provider