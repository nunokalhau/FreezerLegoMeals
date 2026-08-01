using Microsoft.Extensions.Options;
using RAG.DotNet;
using Services.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class OllamaRerankerServiceTests
{
    [Fact]
    public async Task RerankAsync_ReordersCandidatesFromOllamaIds()
    {
        var ollamaClient = new StubOllamaClient("2,1,3");
        var service = new OllamaRerankerService(
            ollamaClient,
            Options.Create(new RerankingOptions { Timeout = TimeSpan.FromSeconds(2) }));

        var candidates = BuildCandidates();
        var result = await service.RerankAsync("best chicken meal", candidates);

        Assert.Equal(new[] { "2", "1", "3" }, result.Select(recipe => recipe.RecipeId).ToArray());
        Assert.Equal(ConversationRole.System, ollamaClient.LastMessages[0].Role);
        Assert.Equal(ConversationRole.User, ollamaClient.LastMessages[1].Role);
        Assert.Empty(ollamaClient.LastTools);
    }

    [Fact]
    public async Task RerankAsync_AppendsMissingIdsFromOriginalOrder()
    {
        var service = new OllamaRerankerService(
            new StubOllamaClient("2"),
            Options.Create(new RerankingOptions { Timeout = TimeSpan.FromSeconds(2) }));

        var result = await service.RerankAsync("best chicken meal", BuildCandidates());

        Assert.Equal(new[] { "2", "1", "3" }, result.Select(recipe => recipe.RecipeId).ToArray());
    }

    [Fact]
    public async Task RerankAsync_WhenTimedOut_ThrowsTimeoutException()
    {
        var service = new OllamaRerankerService(
            new BlockingOllamaClient(),
            Options.Create(new RerankingOptions { Timeout = TimeSpan.FromMilliseconds(20) }));

        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await service.RerankAsync("best chicken meal", BuildCandidates()));
    }

    private static IReadOnlyList<RetrievalRecipe> BuildCandidates()
    {
        return
        [
            new RetrievalRecipe("1", "1", "Spicy Chicken", "desc-1", "tag-1", ["chicken"], "prep-1", "45", 0.8, "canonical-multilingual-projection"),
            new RetrievalRecipe("2", "2", "Beef Stir Fry", "desc-2", "tag-2", ["beef"], "prep-2", "30", 0.7, "canonical-multilingual-projection"),
            new RetrievalRecipe("3", "3", "Garlic Rice", "desc-3", "tag-3", ["rice"], "prep-3", "20", 0.6, "canonical-multilingual-projection")
        ];
    }

    private sealed class StubOllamaClient : IOllamaClient
    {
        private readonly string _content;

        public StubOllamaClient(string content)
        {
            _content = content;
        }

        public IReadOnlyList<ConversationMessage> LastMessages { get; private set; } = [];

        public IReadOnlyList<ToolDefinition> LastTools { get; private set; } = [];

        public Task<OllamaChatResult> ChatAsync(string? model, IReadOnlyList<ConversationMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken = default)
        {
            LastMessages = messages;
            LastTools = tools;
            return Task.FromResult(new OllamaChatResult(_content, []));
        }
    }

    private sealed class BlockingOllamaClient : IOllamaClient
    {
        public async Task<OllamaChatResult> ChatAsync(string? model, IReadOnlyList<ConversationMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new OllamaChatResult(string.Empty, []);
        }
    }
}
