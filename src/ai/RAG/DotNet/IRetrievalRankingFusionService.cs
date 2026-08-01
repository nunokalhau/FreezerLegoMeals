namespace RAG.DotNet;

public interface IRetrievalRankingFusionService
{
    IReadOnlyList<RetrievalRankingEntry> FuseAndCollapse(
        RetrievalProfileDescriptor profile,
        IReadOnlyList<RetrievalRankingEntry> semanticRanking,
        IReadOnlyList<RetrievalRankingEntry> keywordRanking,
        int topK,
        double minimumSimilarity,
        Func<string, string> canonicalIdResolver);
}

public sealed class DefaultRetrievalRankingFusionService : IRetrievalRankingFusionService
{
    private const int ReciprocalRankFusionK = 60;

    public IReadOnlyList<RetrievalRankingEntry> FuseAndCollapse(
        RetrievalProfileDescriptor profile,
        IReadOnlyList<RetrievalRankingEntry> semanticRanking,
        IReadOnlyList<RetrievalRankingEntry> keywordRanking,
        int topK,
        double minimumSimilarity,
        Func<string, string> canonicalIdResolver)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(semanticRanking);
        ArgumentNullException.ThrowIfNull(keywordRanking);
        ArgumentNullException.ThrowIfNull(canonicalIdResolver);

        var profileRanking = profile.ProfileFamily switch
        {
            RetrievalProfileFamily.PerLanguageProjection => BuildPerLanguageRanking(semanticRanking, keywordRanking, minimumSimilarity),
            RetrievalProfileFamily.CanonicalMultilingualProjection => BuildReciprocalRankFusionRanking(semanticRanking, keywordRanking, minimumSimilarity),
            RetrievalProfileFamily.HybridPrecisionRecall => BuildReciprocalRankFusionRanking(semanticRanking, keywordRanking, minimumSimilarity),
            _ => BuildReciprocalRankFusionRanking(semanticRanking, keywordRanking, minimumSimilarity)
        };

        return CanonicalCollapse(profileRanking, canonicalIdResolver)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.RecipeId, StringComparer.Ordinal)
            .Take(topK)
            .ToList();
    }

    private static IReadOnlyList<RetrievalRankingEntry> BuildPerLanguageRanking(
        IReadOnlyList<RetrievalRankingEntry> semanticRanking,
        IReadOnlyList<RetrievalRankingEntry> keywordRanking,
        double minimumSimilarity)
    {
        var results = new List<RetrievalRankingEntry>();
        var added = new HashSet<string>(StringComparer.Ordinal);

        foreach (var semantic in semanticRanking.Where(candidate => candidate.Score >= minimumSimilarity))
        {
            if (added.Add(semantic.RecipeId))
            {
                results.Add(semantic);
            }
        }

        foreach (var keyword in keywordRanking)
        {
            if (added.Add(keyword.RecipeId))
            {
                results.Add(new RetrievalRankingEntry(keyword.RecipeId, keyword.Score / (ReciprocalRankFusionK + 1)));
            }
        }

        return results;
    }

    private static IReadOnlyList<RetrievalRankingEntry> BuildReciprocalRankFusionRanking(
        IReadOnlyList<RetrievalRankingEntry> semanticRanking,
        IReadOnlyList<RetrievalRankingEntry> keywordRanking,
        double minimumSimilarity)
    {
        var semanticEligibleIds = semanticRanking
            .Where(candidate => candidate.Score >= minimumSimilarity)
            .Select(candidate => candidate.RecipeId)
            .ToHashSet(StringComparer.Ordinal);

        var contributions = new Dictionary<string, double>(StringComparer.Ordinal);

        for (var index = 0; index < semanticRanking.Count; index++)
        {
            var entry = semanticRanking[index];
            if (!semanticEligibleIds.Contains(entry.RecipeId))
            {
                continue;
            }

            AddContribution(contributions, entry.RecipeId, index + 1);
        }

        for (var index = 0; index < keywordRanking.Count; index++)
        {
            var entry = keywordRanking[index];
            AddContribution(contributions, entry.RecipeId, index + 1);
        }

        return contributions
            .Select(item => new RetrievalRankingEntry(item.Key, item.Value))
            .ToList();
    }

    private static IReadOnlyList<RetrievalRankingEntry> CanonicalCollapse(
        IReadOnlyList<RetrievalRankingEntry> ranking,
        Func<string, string> canonicalIdResolver)
    {
        var collapsed = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var item in ranking)
        {
            var canonicalId = canonicalIdResolver(item.RecipeId);
            if (string.IsNullOrWhiteSpace(canonicalId))
            {
                canonicalId = item.RecipeId;
            }

            if (collapsed.TryGetValue(canonicalId, out var existingScore))
            {
                collapsed[canonicalId] = Math.Max(existingScore, item.Score);
            }
            else
            {
                collapsed[canonicalId] = item.Score;
            }
        }

        return collapsed
            .Select(item => new RetrievalRankingEntry(item.Key, item.Value))
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
}
