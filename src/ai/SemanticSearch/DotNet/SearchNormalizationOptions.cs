namespace SemanticSearch.DotNet;

public sealed class SearchNormalizationOptions
{
    public string Version { get; init; } = "search-normalization-v1";

    public bool EnableAccentFolding { get; init; } = true;

    public IReadOnlyDictionary<string, IReadOnlyList<string>> AliasMap { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IReadOnlyList<string>> SynonymMap { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> MorphologyMap { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> OcrReplacementMap { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["0"] = "o",
            ["1"] = "l",
            ["5"] = "s"
        };

    public IReadOnlyDictionary<string, string> VoiceReplacementMap { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["wanna"] = "want",
            ["gonna"] = "going",
            ["veggies"] = "vegetables"
        };

    public IReadOnlySet<string> StopwordSet { get; init; }
        = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "and", "as", "at", "by", "for", "from", "in", "into", "made", "making", "of", "on", "or", "the", "to", "using", "use", "with",
            "ao", "aos", "as", "com", "da", "das", "de", "do", "dos", "e", "em", "na", "nas", "no", "nos", "para", "por", "pra", "que", "uma", "um", "usar", "usando", "feito", "feita", "feitos", "feitas"
        };
}
