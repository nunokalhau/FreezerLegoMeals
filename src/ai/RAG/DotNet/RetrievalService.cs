using Domain.DotNet;
using SemanticSearch.DotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Globalization;

namespace RAG.DotNet;

public sealed class RetrievalService : IRetrievalService
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");
    private readonly SemanticSearchService _semanticSearchService;
    private readonly ISemanticRecipeMetadataProvider _metadataProvider;
    private readonly IQueryRewriter? _queryRewriter;
    private readonly IKeywordSearchService? _keywordSearchService;
    private readonly IReranker? _reranker;
    private readonly ISearchQueryNormalizer _searchQueryNormalizer;
    private readonly IRetrievalProfileSelector _profileSelector;
    private readonly IRetrievalRankingFusionService _rankingFusionService;
    private readonly ILogger<RetrievalService> _logger;
    private readonly int _topK;
    private readonly double _minimumSimilarity;

    public RetrievalService(
        SemanticSearchService semanticSearchService,
        ISemanticRecipeMetadataProvider metadataProvider,
        IQueryRewriter? queryRewriter = null,
        IKeywordSearchService? keywordSearchService = null,
        IReranker? reranker = null,
        ISearchQueryNormalizer? searchQueryNormalizer = null,
        IRetrievalProfileSelector? profileSelector = null,
        IRetrievalRankingFusionService? rankingFusionService = null,
        int topK = 3,
        double minimumSimilarity = 0.2,
        ILogger<RetrievalService>? logger = null)
    {
        _semanticSearchService = semanticSearchService ?? throw new ArgumentNullException(nameof(semanticSearchService));
        _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
        _queryRewriter = queryRewriter;
        _keywordSearchService = keywordSearchService;
        _reranker = reranker;
        _searchQueryNormalizer = searchQueryNormalizer ?? new DefaultSearchQueryNormalizer();
        _profileSelector = profileSelector ?? new DefaultRetrievalProfileSelector();
        _rankingFusionService = rankingFusionService ?? new DefaultRetrievalRankingFusionService();
        _topK = topK;
        _minimumSimilarity = minimumSimilarity;
        _logger = logger ?? NullLogger<RetrievalService>.Instance;
    }

    public async Task<RetrievalResult> RetrieveAsync(string question, CancellationToken cancellationToken = default)
    {
        return await RetrieveAsync(
            new RetrievalRequestContext(
                question,
                new RetrievalIntentClassification("GeneralConversation"),
                LocalizationOptions.Create("en"),
                StrictMode: false),
            cancellationToken);
    }

    public async Task<RetrievalResult> RetrieveAsync(
        string question,
        LocalizationOptions localizationOptions,
        CancellationToken cancellationToken = default)
    {
        return await RetrieveAsync(
            new RetrievalRequestContext(
                question,
                new RetrievalIntentClassification("GeneralConversation", DetectedLanguage: localizationOptions.PreferredLanguage),
                localizationOptions,
                localizationOptions.StrictMode),
            cancellationToken);
    }

    public async Task<RetrievalResult> RetrieveAsync(
        RetrievalRequestContext requestContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        ArgumentNullException.ThrowIfNull(requestContext.LocalizationOptions);

        var question = requestContext.OriginalQuestion;
        var localizationOptions = requestContext.LocalizationOptions;

        ArgumentNullException.ThrowIfNull(localizationOptions);

        using var activity = ActivitySource.StartActivity("retrieval.retrieve", ActivityKind.Internal);
        activity?.SetTag("retrieval.top_k", _topK);
        activity?.SetTag("retrieval.minimum_similarity", _minimumSimilarity);
        activity?.SetTag("retrieval.question_length", question?.Length ?? 0);
        activity?.SetTag("retrieval.intent", requestContext.IntentClassification.Intent);
        activity?.SetTag("retrieval.intent_confidence", requestContext.IntentClassification.Confidence);
        activity?.SetTag("retrieval.intent_rule", requestContext.IntentClassification.MatchedRule ?? string.Empty);
        activity?.SetTag("retrieval.intent_detected_language", requestContext.IntentClassification.DetectedLanguage ?? string.Empty);
        activity?.SetTag("retrieval.strict_mode_request", requestContext.StrictMode);

        var selectedProfile = _profileSelector.Select(question ?? string.Empty, localizationOptions);
        activity?.SetTag("retrieval.profile_id", selectedProfile.ProfileId);
        activity?.SetTag("retrieval.profile_family", selectedProfile.ProfileFamily.ToString());
        activity?.SetTag("retrieval.profile_selection_reason", selectedProfile.SelectionReason);
        activity?.SetTag("retrieval.localization.preferred_language", localizationOptions.PreferredLanguage);
        activity?.SetTag("retrieval.localization.strict_mode", localizationOptions.StrictMode);

        var startedAt = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(question))
        {
            LogRetrievalDiagnostics(
                "no-context",
                "empty-query",
                [],
                0,
                startedAt.Elapsed.TotalMilliseconds,
                question?.Length ?? 0);
            activity?.SetTag("retrieval.decision", "no-context");
            activity?.SetTag("retrieval.reason", "empty-query");
            return new RetrievalResult(question ?? string.Empty, [], [], selectedProfile);
        }

        var normalizedQuestion = _searchQueryNormalizer.Normalize(question);
        activity?.SetTag("retrieval.normalization_version", normalizedQuestion.NormalizationVersion);
        activity?.SetTag("retrieval.query_modality", normalizedQuestion.Modality);

        var rewrittenQuestion = question;
        var rewriteStartedAt = Stopwatch.StartNew();
        try
        {
            if (_queryRewriter is not null)
            {
                var rewrittenCandidate = await _queryRewriter.RewriteAsync(question, cancellationToken);
                if (!string.IsNullOrWhiteSpace(rewrittenCandidate))
                {
                    rewrittenQuestion = rewrittenCandidate;
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Query rewrite failed; using original query");
            rewrittenQuestion = question;
        }
        finally
        {
            rewriteStartedAt.Stop();
        }

        _logger.LogInformation(
            "Retrieval query rewrite originalQuery={OriginalQuery} rewrittenQuery={RewrittenQuery} rewriteDurationMs={RewriteDurationMs}",
            question,
            rewrittenQuestion,
            rewriteStartedAt.Elapsed.TotalMilliseconds);
        activity?.SetTag("retrieval.original_query", question);
        activity?.SetTag("retrieval.normalized_query", normalizedQuestion.NormalizedQuery);
        activity?.SetTag("retrieval.rewritten_query", rewrittenQuestion);
        activity?.SetTag("retrieval.rewrite_duration_ms", rewriteStartedAt.Elapsed.TotalMilliseconds);
        activity?.SetTag("retrieval.rewrite_applied", !string.Equals(question, rewrittenQuestion, StringComparison.Ordinal));

        var semanticMatches = await _semanticSearchService.SearchAsync(rewrittenQuestion, _topK, cancellationToken);
        var keywordMatches = _keywordSearchService is null
            ? []
            : await _keywordSearchService.SearchAsync(question, _topK, cancellationToken);

        var semanticRanking = semanticMatches
            .Select(match => new RetrievalRankingEntry(match.RecipeId, match.Score))
            .ToList();
        var keywordRanking = keywordMatches
            .Select(match => new RetrievalRankingEntry(match.RecipeId, match.Score))
            .ToList();
        var fusedRanking = _rankingFusionService.FuseAndCollapse(
            selectedProfile,
            semanticRanking,
            keywordRanking,
            _topK,
            _minimumSimilarity,
            recipeId => recipeId);

        LogRankingDiagnostics("semantic", semanticRanking);
        LogRankingDiagnostics("keyword", keywordRanking);
        LogRankingDiagnostics("fused", fusedRanking);

        activity?.SetTag("retrieval.semantic_ranking", FormatRanking(semanticRanking));
        activity?.SetTag("retrieval.keyword_ranking", FormatRanking(keywordRanking));
        activity?.SetTag("retrieval.fused_ranking", FormatRanking(fusedRanking));

        var similarityScores = semanticMatches.Select(match => match.Score).ToList();
        var elapsedMs = startedAt.Elapsed.TotalMilliseconds;
        if (fusedRanking.Count == 0)
        {
            var noContextReason = semanticMatches.Count == 0 && keywordMatches.Count == 0
                ? "no-semantic-or-keyword-matches"
                : "below-threshold";
            LogRetrievalDiagnostics("no-context", noContextReason, similarityScores, 0, elapsedMs, question.Length);
            activity?.SetTag("retrieval.decision", "no-context");
            activity?.SetTag("retrieval.reason", noContextReason);
            activity?.SetTag("retrieval.semantic_match_count", semanticMatches.Count);
            activity?.SetTag("retrieval.keyword_match_count", keywordMatches.Count);
            activity?.SetTag("retrieval.fused_match_count", fusedRanking.Count);
            activity?.SetTag("retrieval.accepted_count", 0);
            activity?.SetTag("retrieval.elapsed_ms", elapsedMs);
            return new RetrievalResult(question, [], [], selectedProfile, normalizedQuestion.NormalizationVersion);
        }

        var recipes = new List<RetrievalRecipe>();
        foreach (var fusedMatch in fusedRanking)
        {
            var metadata = await GetMetadataAsync(fusedMatch.RecipeId, localizationOptions, cancellationToken);
            if (metadata is null)
            {
                continue;
            }

            var canonicalRecipeId = string.IsNullOrWhiteSpace(metadata.RecipeId) ? fusedMatch.RecipeId : metadata.RecipeId;
            recipes.Add(new RetrievalRecipe(
                fusedMatch.RecipeId,
                canonicalRecipeId,
                metadata.Title,
                metadata.Description,
                metadata.Tags,
                metadata.Ingredients,
                metadata.PreparationSteps,
                metadata.CookingTime,
                fusedMatch.Score,
                selectedProfile.ProfileId,
                metadata.ProjectionSchemaVersion,
                metadata.NormalizationVersion,
                metadata.ProjectionFingerprint,
                metadata.LanguageCoverage));
        }

        recipes = CollapseCanonicalDuplicates(recipes);

        if (recipes.Count == 0)
        {
            var noContextReason = localizationOptions.StrictMode
                ? "localized-projection-missing"
                : "metadata-missing";
            LogRetrievalDiagnostics("no-context", noContextReason, similarityScores, 0, startedAt.Elapsed.TotalMilliseconds, question.Length);
            activity?.SetTag("retrieval.decision", "no-context");
            activity?.SetTag("retrieval.reason", noContextReason);
            activity?.SetTag("retrieval.semantic_match_count", semanticMatches.Count);
            activity?.SetTag("retrieval.keyword_match_count", keywordMatches.Count);
            activity?.SetTag("retrieval.fused_match_count", fusedRanking.Count);
            activity?.SetTag("retrieval.accepted_count", 0);
            activity?.SetTag("retrieval.elapsed_ms", startedAt.Elapsed.TotalMilliseconds);
            return new RetrievalResult(question, [], [], selectedProfile, normalizedQuestion.NormalizationVersion);
        }

        var originalRanking = recipes
            .Select(recipe => new RetrievalRankingEntry(recipe.RecipeId, recipe.SimilarityScore))
            .ToList();
        IReadOnlyList<RetrievalRecipe> rerankedRecipes = recipes;
        var rerankStartedAt = Stopwatch.StartNew();
        try
        {
            if (_reranker is not null && recipes.Count > 1)
            {
                var rerankedCandidates = await _reranker.RerankAsync(question, recipes, cancellationToken);
                rerankedRecipes = NormalizeRerankedCandidates(recipes, rerankedCandidates);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Retrieval reranking failed; preserving original ranking");
            rerankedRecipes = recipes;
        }
        finally
        {
            rerankStartedAt.Stop();
        }

        var rerankedRanking = rerankedRecipes
            .Select(recipe => new RetrievalRankingEntry(recipe.RecipeId, recipe.SimilarityScore))
            .ToList();
        LogRankingDiagnostics("original", originalRanking);
        LogRankingDiagnostics("reranked", rerankedRanking);
        activity?.SetTag("retrieval.original_ranking", FormatRanking(originalRanking));
        activity?.SetTag("retrieval.reranked_ranking", FormatRanking(rerankedRanking));
        activity?.SetTag("retrieval.rerank_duration_ms", rerankStartedAt.Elapsed.TotalMilliseconds);

        var decision = rerankedRecipes.Count == 0 ? "no-context" : "context-accepted";
        var reason = rerankedRecipes.Count == 0 ? "below-threshold" : "threshold-passed";
        elapsedMs = startedAt.Elapsed.TotalMilliseconds;

        LogRetrievalDiagnostics(decision, reason, similarityScores, rerankedRecipes.Count, elapsedMs, question.Length);
        activity?.SetTag("retrieval.decision", decision);
        activity?.SetTag("retrieval.reason", reason);
        activity?.SetTag("retrieval.semantic_match_count", semanticMatches.Count);
        activity?.SetTag("retrieval.keyword_match_count", keywordMatches.Count);
        activity?.SetTag("retrieval.fused_match_count", fusedRanking.Count);
        activity?.SetTag("retrieval.accepted_count", rerankedRecipes.Count);
        activity?.SetTag("retrieval.elapsed_ms", elapsedMs);

        return new RetrievalResult(
            question,
            rerankedRecipes,
            rerankedRecipes.Select(recipe => new SourceAttribution(
                recipe.RecipeId,
                recipe.Title,
                recipe.SimilarityScore,
                recipe.CanonicalRecipeId,
                recipe.RetrievalProfileId)).ToList(),
            selectedProfile,
            normalizedQuestion.NormalizationVersion);
    }

    private async Task<RecipeMetadata?> GetMetadataAsync(
        string recipeId,
        LocalizationOptions localizationOptions,
        CancellationToken cancellationToken)
    {
        if (_metadataProvider is ILocalizedSemanticRecipeMetadataProvider localizedProvider)
        {
            return await localizedProvider.GetMetadataAsync(recipeId, localizationOptions, cancellationToken);
        }

        return await _metadataProvider.GetMetadataAsync(recipeId, cancellationToken);
    }

    private static List<RetrievalRecipe> CollapseCanonicalDuplicates(IReadOnlyList<RetrievalRecipe> recipes)
    {
        return recipes
            .GroupBy(recipe => string.IsNullOrWhiteSpace(recipe.CanonicalRecipeId) ? recipe.RecipeId : recipe.CanonicalRecipeId, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(candidate => candidate.SimilarityScore)
                .First())
            .OrderByDescending(recipe => recipe.SimilarityScore)
            .ThenBy(recipe => recipe.CanonicalRecipeId, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<RetrievalRecipe> NormalizeRerankedCandidates(
        IReadOnlyList<RetrievalRecipe> original,
        IReadOnlyList<RetrievalRecipe>? reranked)
    {
        if (reranked is null || reranked.Count == 0)
        {
            return original;
        }

        var byRecipeId = original
            .GroupBy(recipe => recipe.RecipeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var normalized = new List<RetrievalRecipe>(original.Count);
        foreach (var candidate in reranked)
        {
            if (!byRecipeId.TryGetValue(candidate.RecipeId, out var originalCandidate))
            {
                continue;
            }

            if (normalized.Any(existing => string.Equals(existing.RecipeId, originalCandidate.RecipeId, StringComparison.Ordinal)))
            {
                continue;
            }

            normalized.Add(originalCandidate);
        }

        foreach (var candidate in original)
        {
            if (normalized.Any(existing => string.Equals(existing.RecipeId, candidate.RecipeId, StringComparison.Ordinal)))
            {
                continue;
            }

            normalized.Add(candidate);
        }

        return normalized;
    }

    private void LogRankingDiagnostics(string rankingType, IReadOnlyList<RetrievalRankingEntry> ranking)
    {
        _logger.LogInformation(
            "Retrieval {RankingType} ranking count={Count} items={Items}",
            rankingType,
            ranking.Count,
            FormatRanking(ranking));
    }

    private static string FormatRanking(IReadOnlyList<RetrievalRankingEntry> ranking)
    {
        if (ranking.Count == 0)
            return "none";

        return string.Join(",", ranking.Select((entry, index) =>
            $"r{index + 1}:{entry.RecipeId}:{entry.Score.ToString("F6", CultureInfo.InvariantCulture)}"));
    }

    private void LogRetrievalDiagnostics(
        string decision,
        string reason,
        IReadOnlyList<double> similarityScores,
        int acceptedCount,
        double elapsedMs,
        int questionLength)
    {
        var formattedScores = similarityScores.Count == 0
            ? "none"
            : string.Join(",", similarityScores.Select(score => score.ToString("F6", CultureInfo.InvariantCulture)));

        _logger.LogInformation(
            "Retrieval diagnostics decision={Decision} reason={Reason} questionLength={QuestionLength} topK={TopK} minimumSimilarity={MinimumSimilarity} semanticMatchCount={SemanticMatchCount} acceptedCount={AcceptedCount} similarityScores={SimilarityScores} retrievalLatencyMs={RetrievalLatencyMs}",
            decision,
            reason,
            questionLength,
            _topK,
            _minimumSimilarity,
            similarityScores.Count,
            acceptedCount,
            formattedScores,
            elapsedMs);
    }
}