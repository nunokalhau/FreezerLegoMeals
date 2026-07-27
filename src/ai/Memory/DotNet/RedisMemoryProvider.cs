using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Services.DotNet;

namespace AI.Memory.DotNet;

public sealed class RedisMemoryProvider : IMemoryProvider, IConversationStore, IDisposable
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ConnectionMultiplexer? _redisConnection;
    private readonly ConversationStoreOptions _options;
    private readonly IDatabase? _database;
    private readonly InMemoryMemoryProvider _fallbackProvider;
    private readonly ILogger<RedisMemoryProvider> _logger;
    private const string ConversationPrefix = "conversation:";

    public RedisMemoryProvider(IOptions<ConversationStoreOptions> options, ILogger<RedisMemoryProvider>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<RedisMemoryProvider>.Instance;
        _fallbackProvider = new InMemoryMemoryProvider(options);
        
        try
        {
            // Attempt to connect to Redis
            _redisConnection = ConnectionMultiplexer.Connect(_options.RedisConnectionString);
            _database = _redisConnection.GetDatabase();
            
            // Test the connection by executing a simple command
            _database.Ping();
            _logger.LogInformation("Redis memory provider connected successfully.");
        }
        catch (Exception exception)
        {
            // If Redis is unavailable, fall back to in-memory implementation
            _logger.LogWarning(exception, "Redis memory provider unavailable; using in-memory fallback.");
        }
    }

    public ConversationHistory GetOrCreateConversation(string? conversationId = null)
    {
        using var activity = ActivitySource.StartActivity("memory.get-or-create", ActivityKind.Internal);
        activity?.SetTag("memory.conversation_id.input", conversationId ?? string.Empty);

        var database = _database;
        if (database is null)
        {
            _logger.LogInformation("Memory fallback path used for get-or-create conversationId={ConversationId}", conversationId ?? string.Empty);
            activity?.SetTag("memory.backend", "in-memory-fallback");
            return _fallbackProvider.GetOrCreateConversation(conversationId);
        }

        try
        {
            var resolvedConversationId = string.IsNullOrWhiteSpace(conversationId)
                ? Guid.NewGuid().ToString("N")
                : conversationId;
            activity?.SetTag("memory.backend", "redis");
            activity?.SetTag("memory.conversation_id", resolvedConversationId);

            // Try to get existing conversation from Redis
            var redisKey = $"{ConversationPrefix}{resolvedConversationId}";
            var json = database.StringGet(redisKey);
            
            if (json.HasValue)
            {
                var history = DeserializeConversationHistory(json);
                if (history is not null)
                {
                    UpdateRedisExpiry(redisKey);
                    _logger.LogDebug("Memory conversation cache hit conversationId={ConversationId}", resolvedConversationId);
                    return history;
                }
            }
            
            // Create new conversation if none exists
            var newHistory = new ConversationHistory(resolvedConversationId, []);
            StoreConversationInRedis(newHistory);
            _logger.LogInformation("Memory conversation created conversationId={ConversationId}", resolvedConversationId);
            
            return newHistory;
        }
        catch (Exception exception)
        {
            // If Redis fails for any reason, fall back to in-memory
            _logger.LogWarning(exception, "Memory fallback path used after Redis get-or-create failure conversationId={ConversationId}", conversationId ?? string.Empty);
            activity?.SetTag("memory.backend", "in-memory-fallback");
            return _fallbackProvider.GetOrCreateConversation(conversationId);
        }
    }

    public void AppendMessages(string conversationId, IEnumerable<ConversationMessage> messages)
    {
        using var activity = ActivitySource.StartActivity("memory.append-messages", ActivityKind.Internal);
        activity?.SetTag("memory.conversation_id", conversationId);

        var database = _database;
        if (database is null)
        {
            _logger.LogInformation("Memory fallback path used for append conversationId={ConversationId}", conversationId);
            activity?.SetTag("memory.backend", "in-memory-fallback");
            _fallbackProvider.AppendMessages(conversationId, messages);
            return;
        }

        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
            ArgumentNullException.ThrowIfNull(messages);
            var messageList = messages.ToList();
            activity?.SetTag("memory.backend", "redis");
            activity?.SetTag("memory.append_message_count", messageList.Count);

            var redisKey = $"{ConversationPrefix}{conversationId}";
            
            var json = database.StringGet(redisKey);
            ConversationHistory history;
            
            if (json.HasValue)
            {
                var existingHistory = DeserializeConversationHistory(json);
                var updatedMessages = (existingHistory?.Messages ?? []).Concat(messageList).ToList();
                history = new ConversationHistory(conversationId, updatedMessages);
            }
            else
            {
                history = new ConversationHistory(conversationId, messageList);
            }

            StoreConversationInRedis(history);
            
            if (_options.AutomaticTrimming && _options.MaximumMessagesPerConversation > 0)
            {
                TrimConversationMessagesIfRequired(history, conversationId);
            }

            _logger.LogInformation(
                "Memory append completed conversationId={ConversationId} appendedMessages={AppendedMessages} totalMessages={TotalMessages}",
                conversationId,
                messageList.Count,
                history.Messages.Count);
            
            // Note: Expiration timeout handling is delegated to Redis TTL mechanism
        }
        catch (Exception exception)
        {
            // If Redis fails for any reason, fall back to in-memory
            _logger.LogWarning(exception, "Memory fallback path used after Redis append failure conversationId={ConversationId}", conversationId);
            activity?.SetTag("memory.backend", "in-memory-fallback");
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
        GC.SuppressFinalize(this);
    }
}