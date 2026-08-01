using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RAG.DotNet;

namespace Services.DotNet;

public sealed class OllamaRerankerService : IReranker
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");

    private const string RerankerInstruction =
        "Rank the provided recipe candidates by relevance to the user query. " +
        "Use only the listed candidate recipe IDs. " +
        "Return only a comma-separated list of recipe IDs in best-to-worst order. " +
        "Do not include explanations, labels, or extra text.";

    private static readonly char[] IdSeparators = [',', ';', '|', '>', '\n', '\r', '\t', ' '];

    private readonly IOllamaClient _ollamaClient;
    private readonly RerankingOptions _options;
    private readonly ILogger<OllamaRerankerService> _logger;

    public OllamaRerankerService(
        IOllamaClient ollamaClient,
        IOptions<RerankingOptions> options,
        ILogger<OllamaRerankerService>? logger = null)
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<OllamaRerankerService>.Instance;
    }

    public async Task<IReadOnlyList<RetrievalRecipe>> RerankAsync(
        string query,
        IReadOnlyList<RetrievalRecipe> candidates,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count <= 1)
        {
            return candidates;
        }

        var timeout = _options.Timeout <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(1)
            : _options.Timeout;

        using var activity = ActivitySource.StartActivity("rag.rerank", ActivityKind.Internal);
        activity?.SetTag("rerank.query_length", query.Length);
        activity?.SetTag("rerank.candidate_count", candidates.Count);
        activity?.SetTag("rerank.timeout_ms", timeout.TotalMilliseconds);

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var now = DateTimeOffset.UtcNow;
        var messages = new List<ConversationMessage>
        {
            new(ConversationRole.System, RerankerInstruction, now),
            new(ConversationRole.User, BuildRerankerPrompt(query, candidates), now)
        };

        var startedAt = Stopwatch.StartNew();
        try
        {
            var response = await _ollamaClient.ChatAsync(null, messages, [], linkedCts.Token);
            var orderedIds = ParseOrderedRecipeIds(response.Content, candidates.Select(candidate => candidate.RecipeId).ToList());

            if (orderedIds.Count == 0)
            {
                _logger.LogInformation(
                    "Reranking returned no parsable IDs; preserving original order durationMs={DurationMs}",
                    startedAt.Elapsed.TotalMilliseconds);
                activity?.SetTag("rerank.reordered_count", 0);
                activity?.SetTag("rerank.duration_ms", startedAt.Elapsed.TotalMilliseconds);
                return candidates;
            }

            var candidateById = candidates
                .GroupBy(candidate => candidate.RecipeId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var reordered = new List<RetrievalRecipe>(candidates.Count);
            foreach (var orderedId in orderedIds)
            {
                if (candidateById.TryGetValue(orderedId, out var candidate) &&
                    !reordered.Any(existing => string.Equals(existing.RecipeId, candidate.RecipeId, StringComparison.OrdinalIgnoreCase)))
                {
                    reordered.Add(candidate);
                }
            }

            foreach (var candidate in candidates)
            {
                if (!reordered.Any(existing => string.Equals(existing.RecipeId, candidate.RecipeId, StringComparison.OrdinalIgnoreCase)))
                {
                    reordered.Add(candidate);
                }
            }

            _logger.LogInformation(
                "Reranking completed candidateCount={CandidateCount} reorderedCount={ReorderedCount} durationMs={DurationMs}",
                candidates.Count,
                reordered.Count,
                startedAt.Elapsed.TotalMilliseconds);

            activity?.SetTag("rerank.reordered_count", reordered.Count);
            activity?.SetTag("rerank.duration_ms", startedAt.Elapsed.TotalMilliseconds);
            return reordered;
        }
        catch (OperationCanceledException exception) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "Reranking timed out after {TimeoutMs}ms",
                timeout.TotalMilliseconds);
            activity?.SetTag("rerank.timeout", true);
            activity?.SetTag("rerank.duration_ms", startedAt.Elapsed.TotalMilliseconds);
            throw new TimeoutException($"Reranking timed out after {timeout.TotalMilliseconds}ms", exception);
        }
    }

    private static string BuildRerankerPrompt(string query, IReadOnlyList<RetrievalRecipe> candidates)
    {
        var lines = new List<string>
        {
            $"User Query: {query}",
            "Candidates:"
        };

        foreach (var candidate in candidates)
        {
            var ingredients = candidate.Ingredients.Count == 0
                ? "none"
                : string.Join(", ", candidate.Ingredients);

            lines.Add($"- id={candidate.RecipeId}; title={candidate.Title}; description={candidate.Description}; tags={candidate.Tags}; ingredients={ingredients}");
        }

        lines.Add("Return only IDs as comma-separated values.");
        return string.Join("\n", lines);
    }

    private static IReadOnlyList<string> ParseOrderedRecipeIds(string content, IReadOnlyList<string> candidateIds)
    {
        if (string.IsNullOrWhiteSpace(content) || candidateIds.Count == 0)
        {
            return [];
        }

        var candidateIdSet = candidateIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();

        foreach (var token in content.Split(IdSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = token.Trim().Trim('[', ']', '(', ')', '"', '\'', '`', '.');
            if (!candidateIdSet.Contains(normalized))
            {
                continue;
            }

            if (!ordered.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                ordered.Add(normalized);
            }
        }

        return ordered;
    }
}