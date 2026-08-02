using RAG.DotNet;
using SemanticSearch.DotNet;
using Services.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class QueryRewriterServiceTests
{
    [Fact]
    public async Task RewriteAsync_UsesOllamaClientAndReturnsSingleLineQuery()
    {
        var ollamaClient = new StubOllamaClient("  chicken freezer meal prep with rice\nextra explanation");
        var service = new QueryRewriterService(ollamaClient);

        var rewritten = await service.RewriteAsync("What chicken recipes do you have?");

        Assert.Equal("chicken freezer meal prep rice", rewritten);
        Assert.Equal(2, ollamaClient.LastMessages.Count);
        Assert.Equal(ConversationRole.System, ollamaClient.LastMessages[0].Role);
        Assert.Equal(ConversationRole.User, ollamaClient.LastMessages[1].Role);
        Assert.Contains("Original query: What chicken recipes do you have?", ollamaClient.LastMessages[1].Content);
        Assert.Contains("Normalized query:", ollamaClient.LastMessages[1].Content);
        Assert.Empty(ollamaClient.LastTools);
    }

    [Fact]
    public async Task RewriteAsync_TrimsExcessivelyLongRewrite()
    {
        var longContent = new string('a', 210);
        var service = new QueryRewriterService(new StubOllamaClient(longContent));

        var rewritten = await service.RewriteAsync("Find meals");

        Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", rewritten);
    }

    [Fact]
    public async Task RetrieveAsync_WhenRewriteFails_FallsBackToOriginalQuery()
    {
        var semanticSearch = new SemanticSearchService(
            new RecordingEmbeddingService(),
            new StubVectorStore(0.91),
            new StubMetadataProvider());
        var retrieval = new RetrievalService(
            semanticSearch,
            new StubMetadataProvider(),
            new ThrowingQueryRewriter());

        var result = await retrieval.RetrieveAsync("What chicken recipes do you have?");

        Assert.Single(result.Recipes);
    }

    [Fact]
    public async Task RewriteAsync_SemanticallyEquivalentRewrites_ConvergeToSameCanonicalQuery()
    {
        var ollamaClient = new SequencedStubOllamaClient([
            "recipes with chicken",
            "recipes using chicken"
        ]);
        var service = new QueryRewriterService(ollamaClient);

        var first = await service.RewriteAsync("receitas com frango");
        var second = await service.RewriteAsync("meal prep using chicken");

        Assert.Equal("chicken recipe", first);
        Assert.Equal(first, second);
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

        public Task<OllamaChatResult> ChatAsync(
            string? model,
            IReadOnlyList<ConversationMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            LastMessages = messages;
            LastTools = tools;
            return Task.FromResult(new OllamaChatResult(_content, []));
        }
    }

    private sealed class SequencedStubOllamaClient : IOllamaClient
    {
        private readonly Queue<string> _responses;

        public SequencedStubOllamaClient(IReadOnlyList<string> responses)
        {
            _responses = new Queue<string>(responses);
        }

        public Task<OllamaChatResult> ChatAsync(
            string? model,
            IReadOnlyList<ConversationMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            var content = _responses.Count == 0 ? string.Empty : _responses.Dequeue();
            return Task.FromResult(new OllamaChatResult(content, []));
        }
    }

    private sealed class ThrowingQueryRewriter : IQueryRewriter
    {
        public Task<string> RewriteAsync(string query, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("rewrite failed");
        }
    }

    private sealed class RecordingEmbeddingService : Embedding.DotNet.IEmbeddingService
    {
        public Task<Embedding.DotNet.EmbeddingResponse> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Embedding.DotNet.EmbeddingResponse("test", 2, [1f, 0f]));
        }
    }

    private sealed class StubVectorStore : VectorStores.DotNet.IVectorStore
    {
        private readonly double _score;

        public StubVectorStore(double score)
        {
            _score = score;
        }

        public Task<IReadOnlyList<VectorStores.DotNet.VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VectorStores.DotNet.VectorMatch>>([new VectorStores.DotNet.VectorMatch("1", _score)]);

        public Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpsertAsync(IReadOnlyList<VectorStores.DotNet.VectorDocument> documents, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(IReadOnlyList<string> recipeIds, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearCollectionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubMetadataProvider : SemanticSearch.DotNet.ISemanticRecipeMetadataProvider
    {
        public Task<SemanticSearch.DotNet.RecipeMetadata> GetMetadataAsync(string recipeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SemanticSearch.DotNet.RecipeMetadata(
                recipeId,
                "Spicy Chicken",
                "spicy chicken dinner",
                "Freezer-friendly chicken dinner",
                "spicy, chicken",
                ["chicken", "pepper"],
                "Slice chicken and season it",
                "45"));
    }
}