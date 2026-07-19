using SemanticSearch.DotNet;

namespace RAG.DotNet;

public sealed class RetrievalService : IRetrievalService
{
    private readonly SemanticSearchService _semanticSearchService;
    private readonly ISemanticRecipeMetadataProvider _metadataProvider;
    private readonly int _topK;
    private readonly double _minimumSimilarity;

    public RetrievalService(
        SemanticSearchService semanticSearchService,
        ISemanticRecipeMetadataProvider metadataProvider,
        int topK = 3,
        double minimumSimilarity = 0.2)
    {
        _semanticSearchService = semanticSearchService ?? throw new ArgumentNullException(nameof(semanticSearchService));
        _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
        _topK = topK;
        _minimumSimilarity = minimumSimilarity;
    }

    public async Task<RetrievalResult> RetrieveAsync(string question, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            return new RetrievalResult(question, [], []);

        var matches = await _semanticSearchService.SearchAsync(question, _topK, cancellationToken);
        var recipes = new List<RetrievalRecipe>();
        foreach (var match in matches.Where(match => match.Score >= _minimumSimilarity))
        {
            var metadata = await _metadataProvider.GetMetadataAsync(match.RecipeId, cancellationToken);
            recipes.Add(new RetrievalRecipe(
                match.RecipeId,
                metadata.Title,
                metadata.Description,
                metadata.Tags,
                metadata.Ingredients,
                metadata.PreparationSteps,
                metadata.CookingTime,
                match.Score));
        }

        return new RetrievalResult(
            question,
            recipes,
            recipes.Select(recipe => new SourceAttribution(recipe.RecipeId, recipe.Title, recipe.SimilarityScore)).ToList());
    }
}