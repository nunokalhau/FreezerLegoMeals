using SemanticSearch.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class SearchQueryNormalizerTests
{
    [Fact]
    public void Normalize_IsDeterministicForEquivalentInputs()
    {
        var normalizer = new DefaultSearchQueryNormalizer();

        var first = normalizer.Normalize("  Frango   com   Arroz!! ");
        var second = normalizer.Normalize("frango com arroz");

        Assert.Equal(first.NormalizedQuery, second.NormalizedQuery);
        Assert.Equal(first.ExpandedTokens, second.ExpandedTokens);
    }

    [Fact]
    public void Normalize_AppliesAliasSynonymMorphologyAndVoiceHooks()
    {
        var normalizer = new DefaultSearchQueryNormalizer(new SearchNormalizationOptions
        {
            Version = "phase4-v1",
            AliasMap = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["bbq"] = ["barbecue"]
            },
            SynonymMap = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["chicken"] = ["frango"]
            },
            MorphologyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["veggies"] = "vegetable"
            },
            VoiceReplacementMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["wanna"] = "want"
            }
        });

        var result = normalizer.Normalize("voice: wanna veggies bbq chicken");

        Assert.Equal("voice", result.Modality);
        Assert.Equal("phase4-v1", result.NormalizationVersion);
        Assert.Contains("vegetable", result.NormalizedTokens);
        Assert.Contains("barbecue", result.ExpandedTokens);
        Assert.Contains("frango", result.ExpandedTokens);
        Assert.Contains("want", result.NormalizedTokens);
    }

    [Fact]
    public void Normalize_AppliesOcrHook()
    {
        var normalizer = new DefaultSearchQueryNormalizer();

        var result = normalizer.Normalize("ocr ch1cken r1ce");

        Assert.Equal("ocr", result.Modality);
        Assert.Contains("chlcken", result.NormalizedTokens);
        Assert.Contains("rlce", result.NormalizedTokens);
    }
}
