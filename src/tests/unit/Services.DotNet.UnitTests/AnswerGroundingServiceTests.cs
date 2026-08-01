using RAG.DotNet;
using Services.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class AnswerGroundingServiceTests
{
    [Fact]
    public async Task ValidateAsync_WhenClaimsAreSupported_ReturnsGroundedTrue()
    {
        var service = new AnswerGroundingService();
        var recipes = BuildRecipes();

        var result = await service.ValidateAsync(
            "Use the spicy chicken recipe. It includes chicken and pepper.",
            recipes);

        Assert.True(result.Grounded);
        Assert.Equal(0, result.UnsupportedClaimsCount);
    }

    [Fact]
    public async Task ValidateAsync_WhenClaimsAreUnsupported_ReturnsGroundedFalse()
    {
        var service = new AnswerGroundingService();
        var recipes = BuildRecipes();

        var result = await service.ValidateAsync(
            "This recipe uses salmon and quinoa with dill.",
            recipes);

        Assert.False(result.Grounded);
        Assert.True(result.UnsupportedClaimsCount > 0);
    }

    [Fact]
    public async Task ValidateAsync_WithNoRetrievedRecipes_ReturnsGroundedFalse()
    {
        var service = new AnswerGroundingService();

        var result = await service.ValidateAsync("Use the spicy chicken recipe.", []);

        Assert.False(result.Grounded);
        Assert.Equal(1, result.UnsupportedClaimsCount);
    }

    private static IReadOnlyList<RetrievalRecipe> BuildRecipes() =>
    [
        new RetrievalRecipe(
            "1",
            "Spicy Chicken",
            "Freezer-friendly chicken dinner",
            "spicy",
            ["chicken", "pepper"],
            "Slice chicken and season it",
            "45",
            0.91)
    ];
}
