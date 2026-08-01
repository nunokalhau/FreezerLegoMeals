namespace SemanticSearch.DotNet;

public sealed record SearchNormalizationResult(
    string RawQuery,
    string NormalizedQuery,
    string NormalizationVersion,
    string Modality,
    IReadOnlyList<string> NormalizedTokens,
    IReadOnlyList<string> ExpandedTokens,
    IReadOnlyDictionary<string, IReadOnlyList<string>> AppliedArtifacts);
