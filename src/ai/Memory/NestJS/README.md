# Memory Provider Implementation

This directory contains the conversation memory implementations for Freezer Lego Meals that follow the .NET reference architecture exactly.

## Architecture Overview

The system implements a contract-based approach to conversation memory management, mirroring the .NET Memory Provider pattern:

1. **IMemoryProvider Interface** - Defines the contract for all memory providers
2. **RedisMemoryProvider** - Primary implementation using Redis for distributed storage with fallback capability
3. **InMemoryProvider** - Fallback implementation for development and testing

## Providers

### RedisMemoryProvider

The main memory provider that uses Redis as primary storage:

- Stores conversation histories in Redis with automatic expiration (1 hour by default)
- Falls back gracefully to in-memory storage when Redis is unavailable
- Implements connection retry logic with exponential backoff
- Handles connection timeouts appropriately
- Maintains conversation consistency and thread safety

### InMemoryConversationStore

Fallback provider that maintains conversation state in memory:

- Used as a backup when Redis is unavailable
- Follows the same interface contract to ensure seamless switching
- Implements all required functionality for conversation persistence

## Configuration

The system automatically detects Redis availability at startup. If Redis cannot be reached:
1. Logs warning about Redis unavailability
2. Falls back to in-memory storage automatically
3. Maintains all conversation functionality without interruption

## Usage

The providers are configured through NestJS dependency injection:

```typescript
// In your module configuration
@Module({
  imports: [
    MemoryProviderModule // This registers RedisMemoryProvider as IMemoryProvider 
  ],
  controllers: [YourController],
  providers: [],
})
export class YourModule {}
```

The application will automatically use Redis when available, and fall back to in-memory when necessary.

## Implementation Details

### Redis Key Format
Conversations are stored using the key format: `conversation:{conversationId}`

### Expiration Policy
- All conversations expire after 1 hour (3600 seconds)
- Expiration is refreshed on each access for active conversations

### Fallback Strategy
When Redis connection fails or times out:
1. The system automatically switches to the in-memory fallback provider 
2. No data is lost during the switch - all conversation state remains consistent
3. When Redis becomes available again, the system continues using it seamlessly

## Security Considerations

- Conversation data is stored with no additional encryption
- Ensure Redis server has proper access controls
- For production environments, consider implementing additional data protection measures

## Future Improvements (TODO)

1. Implement Redis connection pooling for better performance in high-concurrency scenarios
2. Add configuration options for expiration times and retry strategies
3. Include optional data encryption for sensitive conversations  
4. Implement Redis clustering support for high availability
5. Add metrics and monitoring for Redis performance
6. Consider implementing different storage strategies (e.g., hybrid Redis + disk)
7. Enhance error handling with circuit breaker patterns for better resiliency

## Testing

Unit tests are included in `/src/tests/unit/Services.NestJS.UnitTests/redis-memory-provider.test.ts`
These tests cover:
- Basic provider instantiation 
- Conversation retrieval and storage
- Error handling and fallback behavior
- Redis client accessibility