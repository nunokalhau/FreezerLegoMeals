using Embedding.DotNet;
using Microsoft.Extensions.Options;
using SemanticSearch.DotNet;
using System.Net;
using System.Text;
using VectorStores.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class SemanticSearchServiceTests
{
    [Fact]
    public void CosineSimilarity_CalculatesExpectedValues()
    {
        Assert.Equal(1, CosineSimilarity.Calculate([1, 0], [1, 0]));
        Assert.Equal(0, CosineSimilarity.Calculate([1, 0], [0, 1]));
        Assert.Equal(0, CosineSimilarity.Calculate([1, 0], []));
    }

    [Fact]
    public async Task ChromaVectorStore_RanksTopKAndReusesCollection()
    {
        var createCalls = 0;
        var queryCalls = 0;
        var queryResponses = new Queue<string>(new[]
        {
            """
            {
              "ids": [["1"]],
              "embeddings": [[[1, 0]]],
              "distances": [[0]],
              "include": ["embeddings", "distances"]
            }
            """,
            """
            {
              "ids": [["1", "2"]],
              "embeddings": [[[1, 0], [0, 1]]],
              "distances": [[0, 1]],
              "include": ["embeddings", "distances"]
            }
            """
        });

        var handler = new StubHttpMessageHandler(async request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/collections", StringComparison.OrdinalIgnoreCase) == true)
            {
                createCalls++;
                var body = await request.Content!.ReadAsStringAsync();
                Assert.Contains("\"name\":\"recipe_embeddings\"", body);
                Assert.Contains("\"get_or_create\":true", body);

                return Json(HttpStatusCode.OK, """
                {
                  "id": "collection-1",
                  "name": "recipe_embeddings",
                  "configuration_json": {},
                  "tenant": "default_tenant",
                  "database": "default_database",
                  "log_position": 0,
                  "version": 1
                }
                """);
            }

            if (request.RequestUri?.AbsolutePath.EndsWith("/query", StringComparison.OrdinalIgnoreCase) == true)
            {
                queryCalls++;
                var body = await request.Content!.ReadAsStringAsync();
                Assert.Contains("\"query_embeddings\":[[1,0]]", body);
                Assert.Contains("\"n_results\":", body);

                return Json(HttpStatusCode.OK, queryResponses.Dequeue());
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var store = new ChromaVectorStore(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:8001")
            },
            Options.Create(new ChromaVectorStoreOptions { CollectionName = "recipe_embeddings" }));

        var matches = await store.SearchAsync([1, 0], 1);
        var cachedMatches = await store.SearchAsync([1, 0], 2);

        Assert.Equal("1", matches.Single().RecipeId);
        Assert.Equal(new[] { "1", "2" }, cachedMatches.Select(match => match.RecipeId));
        Assert.Equal(1, createCalls);
        Assert.Equal(2, queryCalls);
    }

    [Fact]
    public async Task ChromaVectorStore_ReturnsEmptyForEmptyIndex()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/collections", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Task.FromResult(Json(HttpStatusCode.OK, """
                {
                  "id": "collection-1",
                  "name": "recipe_embeddings",
                  "configuration_json": {},
                  "tenant": "default_tenant",
                  "database": "default_database",
                  "log_position": 0,
                  "version": 1
                }
                """));
            }

            if (request.RequestUri?.AbsolutePath.EndsWith("/query", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Task.FromResult(Json(HttpStatusCode.OK, """
                {
                  "ids": [[]],
                  "embeddings": [[]],
                  "distances": [[]],
                  "include": ["embeddings", "distances"]
                }
                """));
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var store = new ChromaVectorStore(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:8001")
            },
            Options.Create(new ChromaVectorStoreOptions()));

        Assert.Empty(await store.SearchAsync([1, 0], 5));
    }

    [Fact]
    public void ChromaVectorStore_WithMissingCollectionName_ThrowsInvalidOperationException()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        Assert.Throws<InvalidOperationException>(() => new ChromaVectorStore(
            httpClient,
            Options.Create(new ChromaVectorStoreOptions { CollectionName = " " })));
    }

    [Fact]
    public async Task ChromaVectorStore_WhenCollectionCreateDoesNotReturnId_ThrowsInvalidOperationException()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/collections", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Task.FromResult(Json(HttpStatusCode.OK, """
                {
                  "name": "recipe_embeddings"
                }
                """));
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var store = new ChromaVectorStore(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:8001")
            },
            Options.Create(new ChromaVectorStoreOptions()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SearchAsync([1, 0], 1));
    }

    [Fact]
    public async Task ChromaVectorStore_WhenEmbeddingsMissing_UsesDistanceFallbackForScore()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/collections", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Task.FromResult(Json(HttpStatusCode.OK, """
                {
                  "id": "collection-1",
                  "name": "recipe_embeddings",
                  "configuration_json": {},
                  "tenant": "default_tenant",
                  "database": "default_database",
                  "log_position": 0,
                  "version": 1
                }
                """));
            }

            if (request.RequestUri?.AbsolutePath.EndsWith("/query", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Task.FromResult(Json(HttpStatusCode.OK, """
                {
                  "ids": [["2", "1"]],
                  "embeddings": null,
                  "distances": [[0.6, 0.1]],
                  "include": ["distances"]
                }
                """));
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var store = new ChromaVectorStore(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:8001")
            },
            Options.Create(new ChromaVectorStoreOptions()));

        var matches = await store.SearchAsync([1, 0], 2);

        Assert.Equal(new[] { "1", "2" }, matches.Select(match => match.RecipeId).ToArray());
        Assert.Equal(0.9, matches[0].Score, 5);
        Assert.Equal(0.4, matches[1].Score, 5);
    }

    [Fact]
    public async Task SemanticSearchService_ReturnsRichRankedResults()
    {
        var service = new SemanticSearchService(
            new StubEmbeddingService(),
            new StubVectorStore(),
            new StubMetadataProvider());

        var results = await service.SearchAsync("spicy dinner", 1);

        var result = Assert.Single(results);
        Assert.Equal("1", result.RecipeId);
        Assert.Equal("Spicy Chicken", result.Title);
        Assert.Equal(1, result.Score);
        Assert.Contains("chicken", result.MatchedText);
        Assert.Contains("High semantic similarity", result.Reason);
    }

    [Fact]
    public async Task SemanticSearchService_ReturnsEmptyForBlankQueryOrInvalidTopK()
    {
        var service = new SemanticSearchService(new StubEmbeddingService(), new StubVectorStore(), new StubMetadataProvider());

        Assert.Empty(await service.SearchAsync(" ", 5));
        Assert.Empty(await service.SearchAsync("anything", 0));
    }

    private sealed class StubEmbeddingService : IEmbeddingService
    {
        public Task<EmbeddingResponse> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmbeddingResponse("test", 2, new[] { 1f, 0f }));
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string payload)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }

    private sealed class StubVectorStore : IVectorStore
    {
        public Task<IReadOnlyList<VectorMatch>> SearchAsync(IReadOnlyList<float> queryEmbedding, int topK, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VectorMatch>>(new[] { new VectorMatch("1", 1) }.Take(topK).ToList());
    }

    private sealed class StubMetadataProvider : ISemanticRecipeMetadataProvider
    {
        public Task<RecipeMetadata> GetMetadataAsync(string recipeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RecipeMetadata(recipeId, "Spicy Chicken", "spicy chicken dinner"));
    }
}