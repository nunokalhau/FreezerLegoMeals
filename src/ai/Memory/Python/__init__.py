"""
Memory provider implementations for Freezer Lego Meals assistant.
This module provides interfaces and implementations for conversation memory management,
matching the .NET RedisMemoryProvider architecture exactly.
"""

from .memory_provider import (
    IMemoryProvider,
    MemoryMessage,
    ConversationMemory,
    InMemoryMemoryProvider,
    get_memory_provider,
    set_memory_provider
)

from .redis_memory_provider import RedisMemoryProvider

__all__ = [
    "IMemoryProvider",
    "MemoryMessage", 
    "ConversationMemory",
    "InMemoryMemoryProvider",
    "RedisMemoryProvider",
    "get_memory_provider",
    "set_memory_provider"
]