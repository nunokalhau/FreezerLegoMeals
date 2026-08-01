using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using RAG.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class RecipeStartupIndexingHostedServiceTests
{
    [Fact]
    public async Task StartAsync_WhenDisabled_DoesNothing()
    {
        var indexingService = new Mock<IRecipeIndexingService>(MockBehavior.Strict);

        var service = new RecipeStartupIndexingHostedService(
            BuildScopeFactory(indexingService.Object),
            Options.Create(new RecipeStartupIndexingOptions
            {
                Enabled = false
            }));

        await service.StartAsync(CancellationToken.None);

        indexingService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartAsync_WhenEnabled_TriggersIndexing()
    {
        var indexingService = new Mock<IRecipeIndexingService>();
        indexingService
            .Setup(candidate => candidate.IndexAllRecipesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecipeIndexingResult(10, 2, 0, "nomic-embed-text", 768, 100));

        var service = new RecipeStartupIndexingHostedService(
            BuildScopeFactory(indexingService.Object),
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
        var indexingService = new Mock<IRecipeIndexingService>();
        indexingService
            .Setup(candidate => candidate.IndexAllRecipesAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);
                return new RecipeIndexingResult(0, 0, 0, string.Empty, 0, 0);
            });

        var service = new RecipeStartupIndexingHostedService(
            BuildScopeFactory(indexingService.Object),
            Options.Create(new RecipeStartupIndexingOptions
            {
                Enabled = true,
                StartupTimeout = TimeSpan.FromMilliseconds(50)
            }));

        await service.StartAsync(CancellationToken.None);
        indexingService.Verify(candidate => candidate.IndexAllRecipesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static IServiceScopeFactory BuildScopeFactory(IRecipeIndexingService indexingService)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => indexingService);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }
}
