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
    private readonly ILogger<RetrievalService> _logger;
    private readonly int _topK;
    private readonly double _minimumSimilarity;

    public RetrievalService(
        SemanticSearchService semanticSearchService,
        ISemanticRecipeMetadataProvider metadataProvider,
        IQueryRewriter? queryRewriter = null,
        int topK = 3,
        double minimumSimilarity = 0.2,
        ILogger<RetrievalService>? logger = null)
    {
        _semanticSearchService = semanticSearchService ?? throw new ArgumentNullException(nameof(semanticSearchService));
        _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
        _queryRewriter = queryRewriter;
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

        var matches = await _semanticSearchService.SearchAsync(rewrittenQuestion, _topK, cancellationToken);
        var similarityScores = matches.Select(match => match.Score).ToList();
        var elapsedMs = startedAt.Elapsed.TotalMilliseconds;
        if (matches.Count == 0)
        {
            LogRetrievalDiagnostics("no-context", "no-semantic-matches", similarityScores, 0, elapsedMs, question.Length);
            activity?.SetTag("retrieval.decision", "no-context");
            activity?.SetTag("retrieval.reason", "no-semantic-matches");
            activity?.SetTag("retrieval.semantic_match_count", 0);
            activity?.SetTag("retrieval.accepted_count", 0);
            activity?.SetTag("retrieval.elapsed_ms", elapsedMs);
            return new RetrievalResult(question, [], []);
        }

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

        var decision = recipes.Count == 0 ? "no-context" : "context-accepted";
        var reason = recipes.Count == 0 ? "below-threshold" : "threshold-passed";
        elapsedMs = startedAt.Elapsed.TotalMilliseconds;

        LogRetrievalDiagnostics(decision, reason, similarityScores, recipes.Count, elapsedMs, question.Length);
        activity?.SetTag("retrieval.decision", decision);
        activity?.SetTag("retrieval.reason", reason);
        activity?.SetTag("retrieval.semantic_match_count", matches.Count);
        activity?.SetTag("retrieval.accepted_count", recipes.Count);
        activity?.SetTag("retrieval.elapsed_ms", elapsedMs);

        return new RetrievalResult(
            question,
            recipes,
            recipes.Select(recipe => new SourceAttribution(recipe.RecipeId, recipe.Title, recipe.SimilarityScore)).ToList());
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