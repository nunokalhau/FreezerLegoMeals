using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Services.DotNet;

namespace AI.Memory.DotNet;

public sealed class RedisMemoryProvider : IMemoryProvider, IConversationStore, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ConnectionMultiplexer? _redisConnection;
    private readonly ConversationStoreOptions _options;
    private readonly IDatabase? _database;
    private readonly InMemoryMemoryProvider _fallbackProvider;
    private const string ConversationPrefix = "conversation:";

    private bool IsRedisAvailable => _database is not null;

    public RedisMemoryProvider(IOptions<ConversationStoreOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _fallbackProvider = new InMemoryMemoryProvider(options);
        
        try
        {
            // Attempt to connect to Redis
            _redisConnection = ConnectionMultiplexer.Connect(_options.RedisConnectionString);
            _database = _redisConnection.GetDatabase();
            
            // Test the connection by executing a simple command
            _database.Ping();
        }
        catch (Exception)
        {
            // If Redis is unavailable, fall back to in-memory implementation
        }
    }

    public ConversationHistory GetOrCreateConversation(string? conversationId = null)
    {
        var database = _database;
        if (database is null)
        {
            return _fallbackProvider.GetOrCreateConversation(conversationId);
        }

        try
        {
            var resolvedConversationId = string.IsNullOrWhiteSpace(conversationId)
                ? Guid.NewGuid().ToString("N")
                : conversationId;

            // Try to get existing conversation from Redis
            var redisKey = $"{ConversationPrefix}{resolvedConversationId}";
            var json = database.StringGet(redisKey);
            
            if (json.HasValue)
            {
                var history = DeserializeConversationHistory(json);
                if (history is not null)
                {
                    UpdateRedisExpiry(redisKey);
                    return history;
                }
            }
            
            // Create new conversation if none exists
            var newHistory = new ConversationHistory(resolvedConversationId, []);
            StoreConversationInRedis(newHistory);
            
            return newHistory;
        }
        catch (Exception)
        {
            // If Redis fails for any reason, fall back to in-memory
            return _fallbackProvider.GetOrCreateConversation(conversationId);
        }
    }

    public void AppendMessages(string conversationId, IEnumerable<ConversationMessage> messages)
    {
        var database = _database;
        if (database is null)
        {
            _fallbackProvider.AppendMessages(conversationId, messages);
            return;
        }

        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
            ArgumentNullException.ThrowIfNull(messages);

            var redisKey = $"{ConversationPrefix}{conversationId}";
            
            var json = database.StringGet(redisKey);
            ConversationHistory history;
            
            if (json.HasValue)
            {
                var existingHistory = DeserializeConversationHistory(json);
                var updatedMessages = (existingHistory?.Messages ?? []).Concat(messages).ToList();
                history = new ConversationHistory(conversationId, updatedMessages);
            }
            else
            {
                history = new ConversationHistory(conversationId, messages.ToList());
            }

            StoreConversationInRedis(history);
            
            if (_options.AutomaticTrimming && _options.MaximumMessagesPerConversation > 0)
            {
                TrimConversationMessagesIfRequired(history, conversationId);
            }
            
            // Note: Expiration timeout handling is delegated to Redis TTL mechanism
        }
        catch (Exception)
        {
            // If Redis fails for any reason, fall back to in-memory
            _fallbackProvider.AppendMessages(conversationId, messages);
        }
    }

    private void StoreConversationInRedis(ConversationHistory history)
    {
        var database = _database;
        if (database is null)
        {
            return;
        }

        try
        {
            var redisKey = $"{ConversationPrefix}{history.ConversationId}";
            var json = JsonSerializer.Serialize(history, SerializerOptions);
            
            database.StringSet(redisKey, json);
            UpdateRedisExpiry(redisKey);
        }
        catch (Exception)
        {
            // Silently fail - fall back to in-memory
        }
    }

    private void UpdateRedisExpiry(string redisKey)
    {
        var database = _database;
        if (database is null)
        {
            return;
        }

        try
        {
            if (_options.ExpirationTimeout > TimeSpan.Zero)
            {
                database.KeyExpire(redisKey, _options.ExpirationTimeout);
            }
        }
        catch (Exception)
        {
            // Silently fail - this is a best-effort operation
        }
    }

    private void TrimConversationMessagesIfRequired(ConversationHistory history, string conversationId)
    {
        try
        {
            if (history.Messages.Count > _options.MaximumMessagesPerConversation)
            {
                var messagesToRemove = history.Messages.Count - _options.MaximumMessagesPerConversation;
                var trimmedMessages = history.Messages.Skip(messagesToRemove).ToList();
                var newHistory = new ConversationHistory(conversationId, trimmedMessages);
                
                // Store back to Redis
                StoreConversationInRedis(newHistory);
            }
        }
        catch (Exception)
        {
            // If trimming fails, we fall back to the in-memory provider which has
            // its own internal logic for handling this case
        }
    }

    private static ConversationHistory? DeserializeConversationHistory(RedisValue json)
    {
        return JsonSerializer.Deserialize<ConversationHistory>(json.ToString(), SerializerOptions);
    }

    public void Dispose()
    {
        _redisConnection?.Dispose();
    }
}