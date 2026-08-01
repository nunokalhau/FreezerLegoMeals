using Domain.DotNet;
using Embedding.DotNet;
using Moq;
using RAG.DotNet;
using Repository.DotNet;
using SemanticSearch.DotNet;
using VectorStores.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class RecipeIndexingServiceTests
{
    [Fact]
    public async Task IndexAllRecipesAsync_LoadsBuildsEmbedsAndUpsertsAllRecipes()
    {
        var recipes = new List<Recipe>
        {
            new()
            {
                Id = 10,
                Name = "Spicy Chicken",
                Notes = "Freezer friendly",
                Tags = "spicy,protein",
                SourcePath = "food/proteins/spicy_chicken.md",
                Servings = 4,
                TimeToPrepare = 45,
                Prepping = "Slice chicken",
                FreezingNotes = "Freeze up to 3 months",
                ReheatNotes = "Microwave 2 minutes",
                Combinations = "Rice + veggies",
                RecipeIngredients =
                [
                    new RecipeIngredient { Amount = 1.5, Unit = "lb", Ingredient = new Ingredient { Name = "Chicken" } },
                    new RecipeIngredient { Amount = 2, Unit = "tbsp", Ingredient = new Ingredient { Name = "Chili powder" } }
                ]
            },
            new()
            {
                Id = 11,
                Name = "Garlic Rice",
                Notes = "Batch side",
                Tags = "starch",
                SourcePath = "food/starches/garlic_rice.md",
                TimeToPrepare = 30,
                Prepping = "Rinse and toast",
                RecipeIngredients =
                [
                    new RecipeIngredient { Amount = 2, Unit = "cup", Ingredient = new Ingredient { Name = "Rice" } }
                ]
            }
        };

        var repository = new Mock<IRecipeIndexingProjectionRepository>();
        repository
            .Setup(candidate => candidate.GetRecipeIndexingProjectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(recipes.Select(CreateProjection).ToList());
        repository
            .Setup(candidate => candidate.UpsertRecipeIndexMetadataAsync(It.IsAny<IReadOnlyList<RecipeIndexMetadataUpsert>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService
            .Setup(candidate => candidate.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) =>
                new EmbeddingResponse("nomic-embed-text", 3, [text.Length, 0f, 1f]));

        var vectorStore = new Mock<IVectorStore>();
        vectorStore.Setup(candidate => candidate.EnsureCollectionExistsAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        vectorStore.Setup(candidate => candidate.UpsertAsync(It.IsAny<IReadOnlyList<VectorDocument>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = new RecipeIndexingService(
            repository.Object,
            embeddingService.Object,
            new RecipeDocumentBuilder(),
            vectorStore.Object,
            new RecipeProjectionFingerprintService(),
            new DefaultSearchQueryNormalizer());

        var result = await service.IndexAllRecipesAsync();

        Assert.Equal(2, result.TotalRecipes);
        Assert.Equal(2, result.IndexedRecipes);
        Assert.Equal(0, result.FailedRecipes);
        Assert.Equal("nomic-embed-text", result.EmbeddingModel);
        Assert.Equal(3, result.EmbeddingDimensions);
        Assert.True(result.DurationMs >= 0);

        vectorStore.Verify(candidate => candidate.EnsureCollectionExistsAsync(It.IsAny<CancellationToken>()), Times.Once);
        embeddingService.Verify(candidate => candidate.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        vectorStore.Verify(candidate => candidate.UpsertAsync(
            It.Is<IReadOnlyList<VectorDocument>>(documents => VerifyUpsertDocuments(documents)),
            It.IsAny<CancellationToken>()),
            Times.Once);
        repository.Verify(candidate => candidate.UpsertRecipeIndexMetadataAsync(
            It.Is<IReadOnlyList<RecipeIndexMetadataUpsert>>(items => items.Count == 2),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IndexAllRecipesAsync_WhenNoRecipes_DoesNotEmbedOrUpsert()
    {
        var repository = new Mock<IRecipeIndexingProjectionRepository>();
        repository
            .Setup(candidate => candidate.GetRecipeIndexingProjectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecipeIndexingProjection>());

        var embeddingService = new Mock<IEmbeddingService>();
        var vectorStore = new Mock<IVectorStore>();
        vectorStore.Setup(candidate => candidate.EnsureCollectionExistsAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = new RecipeIndexingService(repository.Object, embeddingService.Object, new RecipeDocumentBuilder(), vectorStore.Object);

        var result = await service.IndexAllRecipesAsync();

        Assert.Equal(0, result.TotalRecipes);
        Assert.Equal(0, result.IndexedRecipes);
        Assert.Equal(0, result.FailedRecipes);
        Assert.Equal(string.Empty, result.EmbeddingModel);
        Assert.Equal(0, result.EmbeddingDimensions);

        vectorStore.Verify(candidate => candidate.EnsureCollectionExistsAsync(It.IsAny<CancellationToken>()), Times.Once);
        embeddingService.Verify(candidate => candidate.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        vectorStore.Verify(candidate => candidate.UpsertAsync(It.IsAny<IReadOnlyList<VectorDocument>>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(candidate => candidate.UpsertRecipeIndexMetadataAsync(It.IsAny<IReadOnlyList<RecipeIndexMetadataUpsert>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IndexAllRecipesAsync_WhenSingleRecipeEmbeddingFails_ContinuesAndReportsFailure()
    {
        var recipes = new List<Recipe>
        {
            new() { Id = 1, Name = "Good", RecipeIngredients = [new RecipeIngredient { Ingredient = new Ingredient { Name = "Chicken" } }] },
            new() { Id = 2, Name = "Bad", RecipeIngredients = [new RecipeIngredient { Ingredient = new Ingredient { Name = "Pepper" } }] }
        };

        var repository = new Mock<IRecipeIndexingProjectionRepository>();
        repository
            .Setup(candidate => candidate.GetRecipeIndexingProjectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(recipes.Select(CreateProjection).ToList());
        repository
            .Setup(candidate => candidate.UpsertRecipeIndexMetadataAsync(It.IsAny<IReadOnlyList<RecipeIndexMetadataUpsert>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService
            .Setup(candidate => candidate.GenerateEmbeddingAsync(It.Is<string>(value => value.Contains("Title: Bad", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("embedding error"));
        embeddingService
            .Setup(candidate => candidate.GenerateEmbeddingAsync(It.Is<string>(value => !value.Contains("Title: Bad", StringComparison.Ordinal)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse("nomic-embed-text", 2, [1f, 0f]));

        var vectorStore = new Mock<IVectorStore>();
        vectorStore.Setup(candidate => candidate.EnsureCollectionExistsAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        vectorStore.Setup(candidate => candidate.UpsertAsync(It.IsAny<IReadOnlyList<VectorDocument>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = new RecipeIndexingService(repository.Object, embeddingService.Object, new RecipeDocumentBuilder(), vectorStore.Object);

        var result = await service.IndexAllRecipesAsync();

        Assert.Equal(2, result.TotalRecipes);
        Assert.Equal(1, result.IndexedRecipes);
        Assert.Equal(1, result.FailedRecipes);

        vectorStore.Verify(candidate => candidate.UpsertAsync(
            It.Is<IReadOnlyList<VectorDocument>>(documents => documents.Count == 1 && documents[0].RecipeId == "1"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IndexAllRecipesAsync_WhenFingerprintUnchanged_SkipsReindex()
    {
        var recipe = new Recipe
        {
            Id = 12,
            Name = "Rice Bowl",
            Notes = "Stable fixture",
            Tags = "starch",
            Prepping = "Boil rice",
            RecipeIngredients =
            [
                new RecipeIngredient { IngredientId = 3, Amount = 1, Unit = "cup", Ingredient = new Ingredient { Name = "Rice" } }
            ]
        };

        var builder = new RecipeDocumentBuilder();
        var normalizer = new DefaultSearchQueryNormalizer();
        var normalization = normalizer.Normalize("Rice Bowl starch Stable fixture Boil rice Rice");
        var projection = builder.BuildProjection(new RecipeProjectionInput(recipe, normalization.NormalizationVersion, ["en"], []));
        const string fingerprint = "unchanged-fingerprint";

        var repository = new Mock<IRecipeIndexingProjectionRepository>();
        repository
            .Setup(candidate => candidate.GetRecipeIndexingProjectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new RecipeIndexingProjection(
                    recipe,
                    ["en"],
                    ["hash-a"],
                    ["hash-b"],
                    [],
                    [],
                        new RecipeIndexMetadataSnapshot(fingerprint, projection.ProjectionSchemaVersion, "en", DateTime.UtcNow))
            ]);

        var embeddingService = new Mock<IEmbeddingService>(MockBehavior.Strict);
        var vectorStore = new Mock<IVectorStore>();
        vectorStore.Setup(candidate => candidate.EnsureCollectionExistsAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = new RecipeIndexingService(
            repository.Object,
            embeddingService.Object,
            builder,
            vectorStore.Object,
            new FixedFingerprintService(fingerprint),
            normalizer);

        var result = await service.IndexAllRecipesAsync();

        Assert.Equal(1, result.TotalRecipes);
        Assert.Equal(0, result.IndexedRecipes);
        Assert.Equal(0, result.FailedRecipes);
        embeddingService.Verify(candidate => candidate.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        vectorStore.Verify(candidate => candidate.UpsertAsync(It.IsAny<IReadOnlyList<VectorDocument>>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(candidate => candidate.UpsertRecipeIndexMetadataAsync(It.IsAny<IReadOnlyList<RecipeIndexMetadataUpsert>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IndexAllRecipesAsync_WhenFingerprintChanges_ReindexesAndUpdatesMetadata()
    {
        var recipe = new Recipe
        {
            Id = 13,
            Name = "Rice Bowl",
            Notes = "Changed notes",
            Tags = "starch",
            Prepping = "Boil rice",
            RecipeIngredients =
            [
                new RecipeIngredient { IngredientId = 3, Amount = 1, Unit = "cup", Ingredient = new Ingredient { Name = "Rice" } }
            ]
        };

        var repository = new Mock<IRecipeIndexingProjectionRepository>();
        repository
            .Setup(candidate => candidate.GetRecipeIndexingProjectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new RecipeIndexingProjection(
                    recipe,
                    ["en", "pt"],
                    ["hash-a", "hash-c"],
                    ["hash-b"],
                    [],
                    [],
                    new RecipeIndexMetadataSnapshot("old-fingerprint", RecipeDocumentBuilder.DefaultProjectionSchemaVersion, "en", DateTime.UtcNow))
            ]);
        repository
            .Setup(candidate => candidate.UpsertRecipeIndexMetadataAsync(It.IsAny<IReadOnlyList<RecipeIndexMetadataUpsert>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var embeddingService = new Mock<IEmbeddingService>();
        embeddingService
            .Setup(candidate => candidate.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResponse("nomic-embed-text", 2, [1f, 0f]));

        var vectorStore = new Mock<IVectorStore>();
        vectorStore.Setup(candidate => candidate.EnsureCollectionExistsAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        vectorStore.Setup(candidate => candidate.UpsertAsync(It.IsAny<IReadOnlyList<VectorDocument>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = new RecipeIndexingService(repository.Object, embeddingService.Object, new RecipeDocumentBuilder(), vectorStore.Object);

        var result = await service.IndexAllRecipesAsync();

        Assert.Equal(1, result.TotalRecipes);
        Assert.Equal(1, result.IndexedRecipes);
        repository.Verify(candidate => candidate.UpsertRecipeIndexMetadataAsync(
            It.Is<IReadOnlyList<RecipeIndexMetadataUpsert>>(items =>
                items.Count == 1
                && items[0].RecipeId == 13
                && items[0].ProjectionFingerprint != "old-fingerprint"
                && items[0].ProjectionSchemaVersion == RecipeDocumentBuilder.DefaultProjectionSchemaVersion),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static RecipeIndexingProjection CreateProjection(Recipe recipe)
    {
        return new RecipeIndexingProjection(
            recipe,
            ["en"],
            ["recipe-hash"],
            ["ingredient-hash"],
            [],
            [],
            null);
    }

    private sealed class FixedFingerprintService : IRecipeProjectionFingerprintService
    {
        private readonly string _value;

        public FixedFingerprintService(string value)
        {
            _value = value;
        }

        public string Compute(RecipeProjectionFingerprintInput input)
        {
            return _value;
        }
    }

    private static bool VerifyUpsertDocuments(IReadOnlyList<VectorDocument> documents)
    {
        if (documents.Count != 2)
            return false;

        var first = documents[0];
        var second = documents[1];
        if (first.RecipeId != "10" || second.RecipeId != "11")
            return false;

        if (string.IsNullOrWhiteSpace(first.Document))
            return false;

        if (!first.Document.Contains("Title: Spicy Chicken", StringComparison.Ordinal))
            return false;

        if (!first.Document.Contains("Preparation steps: Slice chicken", StringComparison.Ordinal))
            return false;

        if (!first.Document.Contains("Servings: 4", StringComparison.Ordinal))
            return false;

        if (!first.Document.Contains("Ingredients: 1.5 lb Chicken, 2 tbsp Chili powder", StringComparison.Ordinal))
            return false;

        if (first.Metadata is null || second.Metadata is null)
            return false;

        return first.Metadata.ContainsKey("ingredientNames")
            && first.Metadata.ContainsKey("timeToPrepareMinutes")
            && first.Metadata.ContainsKey("hasFreezingNotes")
            && second.Metadata.ContainsKey("sourcePath");
    }
}
