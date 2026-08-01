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
}
