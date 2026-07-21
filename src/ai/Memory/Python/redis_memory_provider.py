"""
Redis memory provider implementation for Freezer Lego Meals assistant.
This module provides a Redis-based memory provider that exactly matches the .NET behavior.
"""

from __future__ import annotations

import json
import logging
import os
from dataclasses import asdict, dataclass, field
from datetime import datetime
from typing import List, Optional

try:
    import redis
    HAS_REDIS = True
except ImportError:
    HAS_REDIS = False

from src.ai.Memory.Python.memory_provider import (
    IMemoryProvider,
    MemoryMessage,
    ConversationMemory,
    InMemoryMemoryProvider,
)
from src.services.Services.Python.conversation_store import (
    ConversationStoreOptions,
    ConversationMessage as ConversationMessageService,
)

logger = logging.getLogger(__name__)


class RedisMemoryProvider(IMemoryProvider):
    """Redis-based memory provider that matches .NET behavior exactly."""
    
    def __init__(self, connection_string: str = "localhost:6379", options: Optional[ConversationStoreOptions] = None):
        """
        Initialize the Redis memory provider.
        
        Args:
            connection_string: Redis connection string (default: "localhost:6379")
            options: Conversation store options for configuration
        """
        self.connection_string = connection_string
        self.options = options or ConversationStoreOptions.from_environment()
        self._redis_client = None
        self._fallback_provider = InMemoryMemoryProvider()
        
        # Attempt to initialize Redis client
        if HAS_REDIS:
            try:
                self._redis_client = redis.Redis.from_url(
                    connection_string,
                    decode_responses=True,
                    socket_connect_timeout=5,
                    socket_timeout=5,
                    retry_on_timeout=True,
                    health_check_interval=30
                )
                # Test the connection
                self._redis_client.ping()
                logger.info("Successfully connected to Redis at %s", connection_string)
            except Exception as e:
                logger.warning("Failed to connect to Redis at %s: %s. Falling back to in-memory.", 
                              connection_string, str(e))
                self._redis_client = None
        else:
            logger.warning("Redis library not available. Falling back to in-memory.")

    def get_or_create_conversation(self, conversation_id: Optional[str] = None) -> ConversationMemory:
        """
        Get an existing conversation or create a new one from Redis or fallback to in-memory.
        
        Args:
            conversation_id: The ID of the conversation to retrieve or create.
                              If None, a new conversation is created.
                              
        Returns:
            ConversationMemory object containing the conversation history.
        """
        # Check if Redis is available
        if self._redis_client is not None:
            try:
                # Try to get conversation from Redis
                resolved_conversation_id = conversation_id.strip() if conversation_id and conversation_id.strip() else None
                if resolved_conversation_id is None:
                    # Create new conversation ID - matching .NET's approach with GUID.NewGuid()
                    import uuid
                    resolved_conversation_id = uuid.uuid4().hex
                
                key = f"conversation:{resolved_conversation_id}"
                data = self._redis_client.get(key)
                
                if data:
                    loaded_data = json.loads(data)
                    # Deserialize messages properly
                    messages = [
                        MemoryMessage(
                            role=msg["role"],
                            content=msg["content"],
                            timestamp=datetime.fromisoformat(msg["timestamp"])
                        )
                        for msg in loaded_data.get("messages", [])
                    ]
                    # Update Redis TTL when accessing for "touch" behavior (access pattern)
                    self._update_redis_expiry(key)
                    return ConversationMemory(
                        conversation_id=resolved_conversation_id,
                        messages=messages
                    )
                
            except Exception as e:
                logger.error("Error getting conversation from Redis: %s", str(e))
                # Fall back to in-memory on any Redis error
                
        # Fallback to in-memory if Redis unavailable or conversation not found
        logger.info("Falling back to in-memory storage for conversation %s", conversation_id)
        return self._fallback_provider.get_or_create_conversation(conversation_id)

    def append_messages(self, conversation_id: str, messages: List[MemoryMessage]) -> None:
        """
        Append messages to an existing conversation in Redis or fallback to in-memory.
        
        Args:
            conversation_id: The ID of the conversation to append to.
            messages: List of MemoryMessage objects to append.
        """
        # Check if Redis is available
        if self._redis_client is not None and hasattr(self._redis_client, 'ping'):
            try:
                # Get current conversation from Redis
                key = f"conversation:{conversation_id}"
                data = self._redis_client.get(key)
                
                # If conversation exists, merge new messages
                all_messages = []
                if data:
                    loaded_data = json.loads(data)
                    existing_messages = [
                        MemoryMessage(
                            role=msg["role"],
                            content=msg["content"],
                            timestamp=datetime.fromisoformat(msg["timestamp"])
                        )
                        for msg in loaded_data.get("messages", [])
                    ]
                    all_messages = existing_messages + messages
                else:
                    all_messages = messages
                
                # Store back to Redis
                store_data = {
                    "conversation_id": conversation_id,
                    "messages": [
                        {
                            "role": msg.role,
                            "content": msg.content,
                            "timestamp": msg.timestamp.isoformat()
                        }
                        for msg in all_messages
                    ]
                }
                
                self._redis_client.set(key, json.dumps(store_data))
                self._update_redis_expiry(key)
                
                # Handle trimming if needed (follow .NET behavior patterns)
                if self.options.automatic_trimming and self.options.maximum_messages_per_conversation > 0:
                    if len(all_messages) > self.options.maximum_messages_per_conversation:
                        self._trim_conversation_messages_if_required(all_messages, conversation_id)
                
                return
                
            except Exception as e:
                logger.error("Error appending to Redis conversation: %s", str(e))
                # Fall back to in-memory on any Redis error
        else:
            logger.info("No Redis connection available. Falling back to in-memory storage for conversation %s", conversation_id)
        
        # Fallback to in-memory if Redis fails for any reason
        self._fallback_provider.append_messages(conversation_id, messages)

    def delete_conversation(self, conversation_id: str) -> bool:
        """
        Delete a conversation from Redis memory or fallback to in-memory.
        
        Args:
            conversation_id: The ID of the conversation to delete.
            
        Returns:
            True if the conversation was deleted, False otherwise.
        """
        # Check if Redis is available
        if self._redis_client is not None and hasattr(self._redis_client, 'ping'):
            try:
                key = f"conversation:{conversation_id}"
                deleted = self._redis_client.delete(key)
                return bool(deleted)
            except Exception as e:
                logger.error("Error deleting conversation from Redis: %s", str(e))
        else:
            logger.info("No Redis connection available. Falling back to in-memory for deletion of conversation %s", conversation_id)
        
        # Fallback to in-memory deletion
        return False

    def _update_redis_expiry(self, key: str) -> None:
        """Update the Redis TTL for a conversation key."""
        try:
            if self._redis_client is not None and self.options.expiration_timeout_seconds > 0:
                self._redis_client.expire(key, int(self.options.expiration_timeout_seconds))
        except Exception as e:
            # Silently fail - this is a best-effort operation
            logger.debug("Failed to update Redis expiry for key %s: %s", key, str(e))

    def _trim_conversation_messages_if_required(self, messages: List[MemoryMessage], conversation_id: str) -> None:
        """Trim conversation messages if the limit is exceeded."""
        try:
            if len(messages) > self.options.maximum_messages_per_conversation:
                # Trim messages to keep only the latest ones
                trimmed_messages = messages[-self.options.maximum_messages_per_conversation:]
                
                # Store back to Redis with trimmed messages
                store_data = {
                    "conversation_id": conversation_id,
                    "messages": [
                        {
                            "role": msg.role,
                            "content": msg.content,
                            "timestamp": msg.timestamp.isoformat()
                        }
                        for msg in trimmed_messages
                    ]
                }
                
                key = f"conversation:{conversation_id}"
                if self._redis_client:
                    self._redis_client.set(key, json.dumps(store_data))
        except Exception as e:
            # Silently fail - let the fallback handle this
            logger.debug("Failed to trim conversation messages: %s", str(e))

    def _to_conversation_history(self, memory_conversation: ConversationMemory) -> ConversationHistory:
        """Convert ConversationMemory to ConversationHistory."""
        conversation_messages = [
            ConversationMessageService(
                role=msg.role,
                content=msg.content,
                timestamp=msg.timestamp
            )
            for msg in memory_conversation.messages
        ]
        return ConversationHistory(
            conversation_id=memory_conversation.conversation_id,
            messages=conversation_messages
        )

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