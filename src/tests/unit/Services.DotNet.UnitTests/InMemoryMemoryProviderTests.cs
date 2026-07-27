using AI.Memory.DotNet;
using Microsoft.Extensions.Options;
using Xunit;

namespace Services.DotNet.UnitTests;

public class InMemoryMemoryProviderTests
{
    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new InMemoryMemoryProvider(null!));
    }

    [Fact]
    public void GetOrCreateConversation_WithoutId_GeneratesConversationId()
    {
        var provider = CreateProvider();

        var conversation = provider.GetOrCreateConversation();

        Assert.False(string.IsNullOrWhiteSpace(conversation.ConversationId));
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void AppendMessages_WithWhitespaceConversationId_ThrowsArgumentException()
    {
        var provider = CreateProvider();

        Assert.Throws<ArgumentException>(() => provider.AppendMessages(" ", []));
    }

    [Fact]
    public void AppendMessages_WithNullMessages_ThrowsArgumentNullException()
    {
        var provider = CreateProvider();

        Assert.Throws<ArgumentNullException>(() => provider.AppendMessages("conversation-1", null!));
    }

    [Fact]
    public void AppendMessages_WhenAutomaticTrimmingEnabled_KeepsMostRecentMessages()
    {
        var provider = CreateProvider(new ConversationStoreOptions
        {
            MaximumMessagesPerConversation = 2,
            AutomaticTrimming = true
        });

        provider.AppendMessages("conversation-1", [
            new ConversationMessage(ConversationRole.User, "first", DateTimeOffset.UtcNow),
            new ConversationMessage(ConversationRole.Assistant, "second", DateTimeOffset.UtcNow),
            new ConversationMessage(ConversationRole.Tool, "third", DateTimeOffset.UtcNow)
        ]);

        var conversation = provider.GetOrCreateConversation("conversation-1");

        Assert.Equal(2, conversation.Messages.Count);
        Assert.Equal("second", conversation.Messages[0].Content);
        Assert.Equal("third", conversation.Messages[1].Content);
    }

    [Fact]
    public void GetOrCreateConversation_WhenMaxConversationLimitExceeded_EvictsLeastRecentlyUsedConversation()
    {
        var provider = CreateProvider(new ConversationStoreOptions
        {
            MaximumConversations = 2,
            AutomaticTrimming = true,
            MaximumMessagesPerConversation = 10
        });

        provider.AppendMessages("one", [new ConversationMessage(ConversationRole.User, "one", DateTimeOffset.UtcNow)]);
        provider.AppendMessages("two", [new ConversationMessage(ConversationRole.User, "two", DateTimeOffset.UtcNow)]);

        // Touch conversation "two" so "one" becomes oldest.
        _ = provider.GetOrCreateConversation("two");

        provider.AppendMessages("three", [new ConversationMessage(ConversationRole.User, "three", DateTimeOffset.UtcNow)]);

        var removed = provider.GetOrCreateConversation("one");
        var retained = provider.GetOrCreateConversation("two");

        Assert.Empty(removed.Messages);
        Assert.Single(retained.Messages);
    }

    private static InMemoryMemoryProvider CreateProvider(ConversationStoreOptions? options = null)
    {
        return new InMemoryMemoryProvider(Options.Create(options ?? new ConversationStoreOptions()));
    }
}
