using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RAG.DotNet;
using SemanticSearch.DotNet;

namespace Services.DotNet;

public sealed class QueryRewriterService : IQueryRewriter
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");

    private const string RewriterInstruction =
        "Rewrite the user message into one concise semantic retrieval query. " +
        "Preserve user intent and entities. Normalize to canonical wording in English. " +
        "Remove conversational filler/function words and keep only retrieval-relevant concepts. " +
        "Do not answer the question. Do not invent facts. " +
        "Return only the rewritten query text with no explanations, prefixes, or formatting.";

    private readonly IOllamaClient _ollamaClient;
    private readonly ISearchQueryNormalizer _searchQueryNormalizer;
    private readonly ILogger<QueryRewriterService> _logger;

    public QueryRewriterService(
        IOllamaClient ollamaClient,
        ISearchQueryNormalizer? searchQueryNormalizer = null,
        ILogger<QueryRewriterService>? logger = null)
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _searchQueryNormalizer = searchQueryNormalizer ?? new DefaultSearchQueryNormalizer();
        _logger = logger ?? NullLogger<QueryRewriterService>.Instance;
    }

    public async Task<string> RewriteAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        using var activity = ActivitySource.StartActivity("rag.query-rewrite", ActivityKind.Internal);
        activity?.SetTag("rewrite.query_length", query.Length);
        var normalizedOriginal = _searchQueryNormalizer.Normalize(query);
        activity?.SetTag("rewrite.normalization_version", normalizedOriginal.NormalizationVersion);
        activity?.SetTag("rewrite.original_normalized_query", normalizedOriginal.NormalizedQuery);

        var now = DateTimeOffset.UtcNow;
        var messages = new List<ConversationMessage>
        {
            new(ConversationRole.System, RewriterInstruction, now),
            new(ConversationRole.User, BuildRewritePrompt(query, normalizedOriginal), now)
        };

        var startedAt = Stopwatch.StartNew();
        var response = await _ollamaClient.ChatAsync(null, messages, [], cancellationToken);
        startedAt.Stop();

        var rewritten = NormalizeToSingleSearchLine(response.Content);
        var normalizedRewritten = _searchQueryNormalizer.Normalize(rewritten);
        var canonical = BuildCanonicalSemanticQuery(normalizedOriginal, normalizedRewritten);

        _logger.LogInformation(
            "Query rewrite completed originalLength={OriginalLength} rewrittenLength={RewrittenLength} canonicalLength={CanonicalLength} durationMs={DurationMs}",
            query.Length,
            rewritten.Length,
            canonical.Length,
            startedAt.Elapsed.TotalMilliseconds);

        activity?.SetTag("rewrite.duration_ms", startedAt.Elapsed.TotalMilliseconds);
        activity?.SetTag("rewrite.rewritten_length", rewritten.Length);
        activity?.SetTag("rewrite.rewritten_normalized_query", normalizedRewritten.NormalizedQuery);
        activity?.SetTag("rewrite.canonical_query", canonical);

        return canonical;
    }

    private static string BuildRewritePrompt(string originalQuery, SearchNormalizationResult normalized)
    {
        var normalizedTokens = normalized.NormalizedTokens.Count == 0
            ? "none"
            : string.Join(", ", normalized.NormalizedTokens);
        var expandedTokens = normalized.ExpandedTokens.Count == 0
            ? "none"
            : string.Join(", ", normalized.ExpandedTokens);

        return string.Join('\n',
        [
            $"Original query: {originalQuery}",
            $"Normalized query: {normalized.NormalizedQuery}",
            $"Normalized tokens: {normalizedTokens}",
            $"Expanded semantic tokens: {expandedTokens}",
            "Return one canonical retrieval query."
        ]);
    }

    private static string BuildCanonicalSemanticQuery(
        SearchNormalizationResult normalizedOriginal,
        SearchNormalizationResult normalizedRewritten)
    {
        var candidateTokens = normalizedRewritten.ExpandedTokens.Count > 0
            ? normalizedRewritten.ExpandedTokens
            : normalizedRewritten.NormalizedTokens;
        if (candidateTokens.Count == 0)
        {
            candidateTokens = normalizedOriginal.ExpandedTokens.Count > 0
                ? normalizedOriginal.ExpandedTokens
                : normalizedOriginal.NormalizedTokens;
        }

        var canonicalTokens = candidateTokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(token => token, StringComparer.Ordinal)
            .ToArray();

        if (canonicalTokens.Length == 0)
        {
            return normalizedOriginal.NormalizedQuery;
        }

        return string.Join(' ', canonicalTokens);
    }

    private static string NormalizeToSingleSearchLine(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        var firstLine = content
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;

        var normalized = firstLine.Trim().Trim('"', '\'', '`');
        if (normalized.Length <= 160)
            return normalized;

        return normalized[..160].TrimEnd();
    }
}