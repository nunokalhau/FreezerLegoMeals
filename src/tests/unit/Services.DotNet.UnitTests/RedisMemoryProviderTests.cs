using AI.Memory.DotNet;
using Microsoft.Extensions.Options;
using Moq;
using Services.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class RedisMemoryProviderTests
{
    [Fact]
    public void GetOrCreateConversation_WithoutId_CreatesNewConversation()
    {
        var store = CreateStore();
        
        var conversation = store.GetOrCreateConversation();
        
        Assert.False(string.IsNullOrWhiteSpace(conversation.ConversationId));
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void GetOrCreateConversation_WithExistingId_ReturnsPersistedMessages()
    {
        var store = CreateStore();
        var conversationId = CreateConversationId();
        var message = CreateMessage("hello");
        store.AppendMessages(conversationId, [message]);

        var conversation = store.GetOrCreateConversation(conversationId);

        Assert.Equal(conversationId, conversation.ConversationId);
        Assert.Single(conversation.Messages);
        Assert.Equal("hello", conversation.Messages[0].Content);
    }

    [Fact]
    public void AppendMessages_PersistsMessagesInOrder()
    {
        var store = CreateStore();
        var conversationId = CreateConversationId();

        store.AppendMessages(conversationId, [CreateMessage("first"), CreateMessage("second")]);

        var conversation = store.GetOrCreateConversation(conversationId);
        Assert.Equal(["first", "second"], conversation.Messages.Select(message => message.Content));
    }

    [Fact]
    public void AppendMessages_TrimsOldMessagesWhenLimitExceeded()
    {
        var store = CreateStore(new ConversationStoreOptions { MaximumMessagesPerConversation = 2 });
        var conversationId = CreateConversationId();

        store.AppendMessages(conversationId, [CreateMessage("first"), CreateMessage("second"), CreateMessage("third")]);

        var conversation = store.GetOrCreateConversation(conversationId);
        Assert.Equal(["second", "third"], conversation.Messages.Select(message => message.Content));
    }

    [Fact]
    public void GetOrCreateConversation_ExpiresOldConversations()
    {
        var store = CreateStore(new ConversationStoreOptions { ExpirationTimeout = TimeSpan.FromMilliseconds(1) });
        var conversationId = CreateConversationId();
        store.AppendMessages(conversationId, [CreateMessage("old")]);

        Thread.Sleep(10);
        var conversation = store.GetOrCreateConversation(conversationId);

        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void Store_SupportsMultipleSimultaneousConversations()
    {
        var store = CreateStore();
        var firstConversationId = CreateConversationId();
        var secondConversationId = CreateConversationId();

        store.AppendMessages(firstConversationId, [CreateMessage("first")]);
        store.AppendMessages(secondConversationId, [CreateMessage("second")]);

        Assert.Equal("first", store.GetOrCreateConversation(firstConversationId).Messages.Single().Content);
        Assert.Equal("second", store.GetOrCreateConversation(secondConversationId).Messages.Single().Content);
    }

    [Fact]
    public void Serialization_Deserialization_PreservesMessageData()
    {
        var store = CreateStore();
        var conversationId = CreateConversationId();
        
        // Store a conversation with multiple message types
        var messages = new[]
        {
            CreateMessage("user message"),
            CreateMessage("assistant response"),
            CreateMessage("tool result")
        };
        
        store.AppendMessages(conversationId, messages);
        
        var conversation = store.GetOrCreateConversation(conversationId);
        
        Assert.Equal(3, conversation.Messages.Count);
        Assert.Equal("user message", conversation.Messages[0].Content);
        Assert.Equal("assistant response", conversation.Messages[1].Content);
        Assert.Equal("tool result", conversation.Messages[2].Content);
    }

    [Fact]
    public void ConversationHistory_Serialization_WithDifferentRoles()
    {
        var store = CreateStore();
        var conversationId = CreateConversationId();
        
        var messages = new[]
        {
            new ConversationMessage(ConversationRole.User, "user message", DateTimeOffset.UtcNow),
            new ConversationMessage(ConversationRole.Assistant, "assistant response", DateTimeOffset.UtcNow),
            new ConversationMessage(ConversationRole.Tool, "tool result", DateTimeOffset.UtcNow),
            new ConversationMessage(ConversationRole.System, "system message", DateTimeOffset.UtcNow)
        };
        
        store.AppendMessages(conversationId, messages);
        
        var conversation = store.GetOrCreateConversation(conversationId);
        
        Assert.Equal(4, conversation.Messages.Count);
        Assert.Equal(ConversationRole.User, conversation.Messages[0].Role);
        Assert.Equal(ConversationRole.Assistant, conversation.Messages[1].Role);
        Assert.Equal(ConversationRole.Tool, conversation.Messages[2].Role);
        Assert.Equal(ConversationRole.System, conversation.Messages[3].Role);
    }

    [Fact]
    public void SupportsConcurrentAccess()
    {
        var store = CreateStore();
        var conversationIds = Enumerable.Range(0, 5).Select(_ => CreateConversationId()).ToArray();
        
        // Multiple threads appending messages to separate conversations
        var tasks = new List<Task>();
        
        for (int i = 0; i < 5; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(() =>
            {
                store.AppendMessages(conversationIds[threadId], [CreateMessage($"message-from-thread-{threadId}")]);
            }));
        }
        
        Task.WaitAll(tasks.ToArray());
        
        // Verify all conversations have their messages
        for (int i = 0; i < 5; i++)
        {
            var conversation = store.GetOrCreateConversation(conversationIds[i]);
            Assert.Single(conversation.Messages);
            Assert.Equal($"message-from-thread-{i}", conversation.Messages[0].Content);
        }
    }

    private static RedisMemoryProvider CreateStore(ConversationStoreOptions? options = null)
    {
        var mockOptions = new Mock<IOptions<ConversationStoreOptions>>();
        mockOptions.Setup(o => o.Value).Returns(options ?? new ConversationStoreOptions());
        
        return new RedisMemoryProvider(mockOptions.Object);
    }

    private static ConversationMessage CreateMessage(string content)
    {
        return new ConversationMessage(ConversationRole.User, content, DateTimeOffset.UtcNow);
    }

    private static string CreateConversationId()
    {
        return $"conversation-{Guid.NewGuid():N}";
    }
}