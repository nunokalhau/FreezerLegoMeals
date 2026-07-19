using Microsoft.Extensions.Options;
using Xunit;

namespace Services.DotNet.UnitTests;

public class InMemoryConversationStoreTests
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
        store.AppendMessages("conversation-1", [CreateMessage("hello")]);

        var conversation = store.GetOrCreateConversation("conversation-1");

        Assert.Equal("conversation-1", conversation.ConversationId);
        Assert.Single(conversation.Messages);
        Assert.Equal("hello", conversation.Messages[0].Content);
    }

    [Fact]
    public void AppendMessages_PersistsMessagesInOrder()
    {
        var store = CreateStore();

        store.AppendMessages("conversation-1", [CreateMessage("first"), CreateMessage("second")]);

        var conversation = store.GetOrCreateConversation("conversation-1");
        Assert.Equal(["first", "second"], conversation.Messages.Select(message => message.Content));
    }

    [Fact]
    public void AppendMessages_TrimsOldMessagesWhenLimitExceeded()
    {
        var store = CreateStore(new ConversationStoreOptions { MaximumMessagesPerConversation = 2 });

        store.AppendMessages("conversation-1", [CreateMessage("first"), CreateMessage("second"), CreateMessage("third")]);

        var conversation = store.GetOrCreateConversation("conversation-1");
        Assert.Equal(["second", "third"], conversation.Messages.Select(message => message.Content));
    }

    [Fact]
    public void GetOrCreateConversation_ExpiresOldConversations()
    {
        var store = CreateStore(new ConversationStoreOptions { ExpirationTimeout = TimeSpan.FromMilliseconds(1) });
        store.AppendMessages("conversation-1", [CreateMessage("old")]);

        Thread.Sleep(10);
        var conversation = store.GetOrCreateConversation("conversation-1");

        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void Store_SupportsMultipleSimultaneousConversations()
    {
        var store = CreateStore();

        store.AppendMessages("conversation-1", [CreateMessage("first")]);
        store.AppendMessages("conversation-2", [CreateMessage("second")]);

        Assert.Equal("first", store.GetOrCreateConversation("conversation-1").Messages.Single().Content);
        Assert.Equal("second", store.GetOrCreateConversation("conversation-2").Messages.Single().Content);
    }

    private static InMemoryConversationStore CreateStore(ConversationStoreOptions? options = null)
    {
        return new InMemoryConversationStore(Options.Create(options ?? new ConversationStoreOptions()));
    }

    private static ConversationMessage CreateMessage(string content)
    {
        return new ConversationMessage(ConversationRole.User, content, DateTimeOffset.UtcNow);
    }
}