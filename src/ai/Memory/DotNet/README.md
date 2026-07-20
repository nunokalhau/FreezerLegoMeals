# RedisMemoryProvider

The `RedisMemoryProvider` is an implementation of the `IMemoryProvider` interface that provides persistent conversation memory storage with a fallback to in-memory storage when Redis is unavailable.

## How it Works

The provider uses Redis as the primary storage mechanism for conversation history, preserving message order, roles, and timestamps. When Redis is unavailable, it automatically falls back to an in-memory implementation to ensure uninterrupted operation.

## Connection String Handling

The provider reads its Redis connection from `ConversationStore:RedisConnectionString`. The default application setting is `localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000`. If this connection fails, the fallback mechanism activates immediately, allowing the system to continue operating with local memory storage.

## Conversation Persistence

Conversation history is persisted with all key attributes preserved:

1. **Message Order**: Messages are stored and retrieved in the same sequence they were added
2. **Roles**: Each message maintains its original role (System, User, Assistant, or Tool)
3. **Timestamps**: All messages include their creation timestamps for temporal ordering

## Fallback Logic

When Redis is unreachable or fails during operation, the provider automatically delegates to an `InMemoryMemoryProvider`. This ensures that:

- Conversations can still be created and managed
- Users experience no interruption in functionality
- The system maintains consistent behavior regardless of Redis availability

## Future Improvements

The current implementation has several planned enhancements:

1. **TTL Support**: Implement time-to-live for conversations to automatically clean up old data
2. **Summarization**: Add conversation summarization capabilities to reduce storage requirements for long sessions
3. **Distributed Caching**: Enhance scalability through better distributed cache patterns
4. **Configurable Connection**: Fine-tune connection and resilience settings per environment

The provider follows the principle of graceful degradation, ensuring that core functionality remains available even when optional features are not accessible.