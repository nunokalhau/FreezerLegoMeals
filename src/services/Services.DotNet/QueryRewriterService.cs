using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RAG.DotNet;

namespace Services.DotNet;

public sealed class QueryRewriterService : IQueryRewriter
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");

    private const string RewriterInstruction =
        "Rewrite the user message into one concise semantic search query. " +
        "Preserve the original intent. Do not answer the question. Do not invent facts. " +
        "Return only the rewritten search query text with no explanations, prefixes, or formatting.";

    private readonly IOllamaClient _ollamaClient;
    private readonly ILogger<QueryRewriterService> _logger;

    public QueryRewriterService(IOllamaClient ollamaClient, ILogger<QueryRewriterService>? logger = null)
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _logger = logger ?? NullLogger<QueryRewriterService>.Instance;
    }

    public async Task<string> RewriteAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        using var activity = ActivitySource.StartActivity("rag.query-rewrite", ActivityKind.Internal);
        activity?.SetTag("rewrite.query_length", query.Length);

        var now = DateTimeOffset.UtcNow;
        var messages = new List<ConversationMessage>
        {
            new(ConversationRole.System, RewriterInstruction, now),
            new(ConversationRole.User, query, now)
        };

        var startedAt = Stopwatch.StartNew();
        var response = await _ollamaClient.ChatAsync(null, messages, [], cancellationToken);
        startedAt.Stop();

        var rewritten = NormalizeToSingleSearchLine(response.Content);

        _logger.LogInformation(
            "Query rewrite completed originalLength={OriginalLength} rewrittenLength={RewrittenLength} durationMs={DurationMs}",
            query.Length,
            rewritten.Length,
            startedAt.Elapsed.TotalMilliseconds);

        activity?.SetTag("rewrite.duration_ms", startedAt.Elapsed.TotalMilliseconds);
        activity?.SetTag("rewrite.rewritten_length", rewritten.Length);

        return rewritten;
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