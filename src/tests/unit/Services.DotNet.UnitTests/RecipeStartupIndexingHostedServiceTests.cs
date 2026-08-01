using Embedding.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using RAG.DotNet;
using VectorStores.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class RecipeStartupIndexingHostedServiceTests
{
    [Fact]
    public async Task StartAsync_WhenDisabled_DoesNothing()
    {
        var vectorStore = new Mock<IVectorStore>(MockBehavior.Strict);
        var embeddingService = new Mock<IEmbeddingService>(MockBehavior.Strict);
        var indexingService = new Mock<IRecipeIndexingService>(MockBehavior.Strict);

        var service = new RecipeStartupIndexingHostedService(
            BuildScopeFactory(vectorStore.Object, embeddingService.Object, indexingService.Object),
            Options.Create(new RecipeStartupIndexingOptions
            {
                Enabled = false
            }));

        await service.StartAsync(CancellationToken.None);

        vectorStore.VerifyNoOtherCalls();
        embeddingService.VerifyNoOtherCalls();
        indexingService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartAsync_WhenIndexAlreadyHasData_SkipsReindex()
    {
        var vectorStore = new Mock<IVectorStore>();
        vectorStore.Setup(candidate => candidate.EnsureCollectionExistsAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        vectorStore
            .Setup(candidate => candidate.SearchAsync(It.IsAny<IReadOnlyList<float>>(), 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new VectorMatch("1", 0.8)]);

        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService
            .Setup(candidate => candidate.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse("nomic-embed-text", 2, [1f, 0f]));

        var indexingService = new Mock<IRecipeIndexingService>();

        var service = new RecipeStartupIndexingHostedService(
            BuildScopeFactory(vectorStore.Object, embeddingService.Object, indexingService.Object),
            Options.Create(new RecipeStartupIndexingOptions
            {
                Enabled = true,
                StartupTimeout = TimeSpan.FromSeconds(5),
                ProbeTopK = 1
            }));

        await service.StartAsync(CancellationToken.None);

        vectorStore.Verify(candidate => candidate.EnsureCollectionExistsAsync(It.IsAny<CancellationToken>()), Times.Once);
        embeddingService.Verify(candidate => candidate.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        vectorStore.Verify(candidate => candidate.SearchAsync(It.IsAny<IReadOnlyList<float>>(), 1, It.IsAny<CancellationToken>()), Times.Once);
        indexingService.Verify(candidate => candidate.IndexAllRecipesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_WhenIndexEmpty_TriggersReindex()
    {
        var vectorStore = new Mock<IVectorStore>();
        vectorStore.Setup(candidate => candidate.EnsureCollectionExistsAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        vectorStore
            .Setup(candidate => candidate.SearchAsync(It.IsAny<IReadOnlyList<float>>(), 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<VectorMatch>());

        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService
            .Setup(candidate => candidate.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse("nomic-embed-text", 2, [1f, 0f]));

        var indexingService = new Mock<IRecipeIndexingService>();
        indexingService
            .Setup(candidate => candidate.IndexAllRecipesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecipeIndexingResult(10, 10, 0, "nomic-embed-text", 768, 100));

        var service = new RecipeStartupIndexingHostedService(
            BuildScopeFactory(vectorStore.Object, embeddingService.Object, indexingService.Object),
            Options.Create(new RecipeStartupIndexingOptions
            {
                Enabled = true,
                StartupTimeout = TimeSpan.FromSeconds(5)
            }));

        await service.StartAsync(CancellationToken.None);

        indexingService.Verify(candidate => candidate.IndexAllRecipesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WhenOperationTimesOut_DoesNotThrow()
    {
        var vectorStore = new Mock<IVectorStore>();
        vectorStore
            .Setup(candidate => candidate.EnsureCollectionExistsAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(token => Task.Delay(TimeSpan.FromSeconds(5), token));

        var embeddingService = new Mock<IEmbeddingService>(MockBehavior.Strict);
        var indexingService = new Mock<IRecipeIndexingService>(MockBehavior.Strict);

        var service = new RecipeStartupIndexingHostedService(
            BuildScopeFactory(vectorStore.Object, embeddingService.Object, indexingService.Object),
            Options.Create(new RecipeStartupIndexingOptions
            {
                Enabled = true,
                StartupTimeout = TimeSpan.FromMilliseconds(50)
            }));

        await service.StartAsync(CancellationToken.None);

        embeddingService.VerifyNoOtherCalls();
        indexingService.VerifyNoOtherCalls();
    }

    private static IServiceScopeFactory BuildScopeFactory(
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        IRecipeIndexingService indexingService)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => vectorStore);
        services.AddScoped(_ => embeddingService);
        services.AddScoped(_ => indexingService);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }
}
