using SemanticSearch.DotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Globalization;

namespace RAG.DotNet;

public sealed class RetrievalService : IRetrievalService
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");
    private const int ReciprocalRankFusionK = 60;
    private readonly SemanticSearchService _semanticSearchService;
    private readonly ISemanticRecipeMetadataProvider _metadataProvider;
    private readonly IQueryRewriter? _queryRewriter;
    private readonly IKeywordSearchService? _keywordSearchService;
    private readonly IReranker? _reranker;
    private readonly ILogger<RetrievalService> _logger;
    private readonly int _topK;
    private readonly double _minimumSimilarity;

    public RetrievalService(
        SemanticSearchService semanticSearchService,
        ISemanticRecipeMetadataProvider metadataProvider,
        IQueryRewriter? queryRewriter = null,
        IKeywordSearchService? keywordSearchService = null,
        IReranker? reranker = null,
        int topK = 3,
        double minimumSimilarity = 0.2,
        ILogger<RetrievalService>? logger = null)
    {
        _semanticSearchService = semanticSearchService ?? throw new ArgumentNullException(nameof(semanticSearchService));
        _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
        _queryRewriter = queryRewriter;
        _keywordSearchService = keywordSearchService;
        _reranker = reranker;
        _topK = topK;
        _minimumSimilarity = minimumSimilarity;
        _logger = logger ?? NullLogger<RetrievalService>.Instance;
    }

    public async Task<RetrievalResult> RetrieveAsync(string question, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("retrieval.retrieve", ActivityKind.Internal);
        activity?.SetTag("retrieval.top_k", _topK);
        activity?.SetTag("retrieval.minimum_similarity", _minimumSimilarity);
        activity?.SetTag("retrieval.question_length", question?.Length ?? 0);

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
            return new RetrievalResult(question ?? string.Empty, [], []);
        }

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
        activity?.SetTag("retrieval.rewritten_query", rewrittenQuestion);
        activity?.SetTag("retrieval.rewrite_duration_ms", rewriteStartedAt.Elapsed.TotalMilliseconds);
        activity?.SetTag("retrieval.rewrite_applied", !string.Equals(question, rewrittenQuestion, StringComparison.Ordinal));

        var semanticMatches = await _semanticSearchService.SearchAsync(rewrittenQuestion, _topK, cancellationToken);
        var keywordMatches = _keywordSearchService is null
            ? []
            : await _keywordSearchService.SearchAsync(question, _topK, cancellationToken);

        var semanticRanking = semanticMatches
            .Select(match => new RankedRecipe(match.RecipeId, match.Score))
            .ToList();
        var keywordRanking = keywordMatches
            .Select(match => new RankedRecipe(match.RecipeId, match.Score))
            .ToList();
        var fusedRanking = FuseRankings(semanticRanking, keywordRanking);

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
            return new RetrievalResult(question, [], []);
        }

        var recipes = new List<RetrievalRecipe>();
        foreach (var fusedMatch in fusedRanking)
        {
            var metadata = await _metadataProvider.GetMetadataAsync(fusedMatch.RecipeId, cancellationToken);
            recipes.Add(new RetrievalRecipe(
                fusedMatch.RecipeId,
                metadata.Title,
                metadata.Description,
                metadata.Tags,
                metadata.Ingredients,
                metadata.PreparationSteps,
                metadata.CookingTime,
                fusedMatch.Score));
        }

        var originalRanking = recipes
            .Select(recipe => new RankedRecipe(recipe.RecipeId, recipe.SimilarityScore))
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
            .Select(recipe => new RankedRecipe(recipe.RecipeId, recipe.SimilarityScore))
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
            rerankedRecipes.Select(recipe => new SourceAttribution(recipe.RecipeId, recipe.Title, recipe.SimilarityScore)).ToList());
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

    private List<RankedRecipe> FuseRankings(
        IReadOnlyList<RankedRecipe> semanticRanking,
        IReadOnlyList<RankedRecipe> keywordRanking)
    {
        var semanticEligibleIds = semanticRanking
            .Where(candidate => candidate.Score >= _minimumSimilarity)
            .Select(candidate => candidate.RecipeId)
            .ToHashSet(StringComparer.Ordinal);

        var contributions = new Dictionary<string, double>(StringComparer.Ordinal);

        for (var index = 0; index < semanticRanking.Count; index++)
        {
            var entry = semanticRanking[index];
            if (!semanticEligibleIds.Contains(entry.RecipeId))
                continue;

            AddContribution(contributions, entry.RecipeId, index + 1);
        }

        for (var index = 0; index < keywordRanking.Count; index++)
        {
            var entry = keywordRanking[index];
            AddContribution(contributions, entry.RecipeId, index + 1);
        }

        return contributions
            .Select(item => new RankedRecipe(item.Key, item.Value))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.RecipeId, StringComparer.Ordinal)
            .Take(_topK)
            .ToList();
    }

    private static void AddContribution(IDictionary<string, double> contributions, string recipeId, int rank)
    {
        var rrfScore = 1d / (ReciprocalRankFusionK + rank);
        if (contributions.TryGetValue(recipeId, out var existing))
        {
            contributions[recipeId] = existing + rrfScore;
        }
        else
        {
            contributions[recipeId] = rrfScore;
        }
    }

    private void LogRankingDiagnostics(string rankingType, IReadOnlyList<RankedRecipe> ranking)
    {
        _logger.LogInformation(
            "Retrieval {RankingType} ranking count={Count} items={Items}",
            rankingType,
            ranking.Count,
            FormatRanking(ranking));
    }

    private static string FormatRanking(IReadOnlyList<RankedRecipe> ranking)
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

    private sealed record RankedRecipe(string RecipeId, double Score);
}