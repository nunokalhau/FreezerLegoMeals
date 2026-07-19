using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Services.DotNet;

// TODO: Replace InMemoryConversationStore with a Redis-backed implementation.
// TODO: Persist conversation history using Redis running in Docker.
// TODO: Support distributed conversation storage for multiple API instances.
public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<string, ConversationState> _conversations = new();
    private readonly ConversationStoreOptions _options;

    public InMemoryConversationStore(IOptions<ConversationStoreOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public ConversationHistory GetOrCreateConversation(string? conversationId = null)
    {
        ExpireOldConversations();

        var resolvedConversationId = string.IsNullOrWhiteSpace(conversationId)
            ? Guid.NewGuid().ToString("N")
            : conversationId;

        var state = _conversations.GetOrAdd(resolvedConversationId, _ => new ConversationState());
        lock (state)
        {
            state.LastAccessedAt = DateTimeOffset.UtcNow;
            return new ConversationHistory(resolvedConversationId, state.Messages.ToList());
        }
    }

    public void AppendMessages(string conversationId, IEnumerable<ConversationMessage> messages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(messages);

        var state = _conversations.GetOrAdd(conversationId, _ => new ConversationState());
        lock (state)
        {
            state.Messages.AddRange(messages);
            state.LastAccessedAt = DateTimeOffset.UtcNow;

            if (_options.AutomaticTrimming && _options.MaximumMessagesPerConversation > 0)
            {
                var messagesToRemove = state.Messages.Count - _options.MaximumMessagesPerConversation;
                if (messagesToRemove > 0)
                {
                    state.Messages.RemoveRange(0, messagesToRemove);
                }
            }
        }

        EnforceConversationLimit();
    }

    private void ExpireOldConversations()
    {
        if (_options.ExpirationTimeout <= TimeSpan.Zero)
        {
            return;
        }

        var expiresBefore = DateTimeOffset.UtcNow.Subtract(_options.ExpirationTimeout);
        foreach (var item in _conversations)
        {
            if (item.Value.LastAccessedAt < expiresBefore)
            {
                _conversations.TryRemove(item.Key, out _);
            }
        }
    }

    private void EnforceConversationLimit()
    {
        if (_options.MaximumConversations <= 0)
        {
            return;
        }

        var conversationsToRemove = _conversations.Count - _options.MaximumConversations;
        if (conversationsToRemove <= 0)
        {
            return;
        }

        foreach (var item in _conversations.OrderBy(item => item.Value.LastAccessedAt).Take(conversationsToRemove))
        {
            _conversations.TryRemove(item.Key, out _);
        }
    }

    private sealed class ConversationState
    {
        public List<ConversationMessage> Messages { get; } = [];

        public DateTimeOffset LastAccessedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}