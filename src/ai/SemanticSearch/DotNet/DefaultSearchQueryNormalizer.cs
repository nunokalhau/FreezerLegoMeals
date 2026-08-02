using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SemanticSearch.DotNet;

public sealed class DefaultSearchQueryNormalizer : ISearchQueryNormalizer
{
    private static readonly Regex NonWordPattern = new("[^\\p{L}\\p{N}\\s]", RegexOptions.Compiled);
    private static readonly Regex WhitespacePattern = new("\\s+", RegexOptions.Compiled);

    private readonly SearchNormalizationOptions _options;

    public DefaultSearchQueryNormalizer(SearchNormalizationOptions? options = null)
    {
        _options = options ?? new SearchNormalizationOptions();
    }

    public SearchNormalizationResult Normalize(string query)
    {
        var rawQuery = query ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return new SearchNormalizationResult(
                rawQuery,
                string.Empty,
                _options.Version,
                "text",
                [],
                [],
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));
        }

        var modality = DetectModality(rawQuery);
        var normalized = NormalizeCore(rawQuery);
        var tokens = Tokenize(normalized);

        var artifactMap = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["ocr"] = [],
            ["voice"] = [],
            ["stopword"] = [],
            ["morphology"] = [],
            ["alias"] = [],
            ["synonym"] = []
        };

        var modalityTokens = ApplyModalityHooks(tokens, modality, artifactMap);
        var stopwordFilteredTokens = ApplyStopwordFilter(modalityTokens, artifactMap);
        var morphologyTokens = ApplyMorphology(stopwordFilteredTokens, artifactMap);
        var expandedTokens = ExpandAliasesAndSynonyms(morphologyTokens, artifactMap);

        var normalizedTokens = DistinctPreservingOrder(morphologyTokens);
        var expandedNormalizedTokens = DistinctPreservingOrder(expandedTokens);

        return new SearchNormalizationResult(
            rawQuery,
            string.Join(' ', normalizedTokens),
            _options.Version,
            modality,
            normalizedTokens,
            expandedNormalizedTokens,
            artifactMap);
    }

    private IReadOnlyList<string> ApplyModalityHooks(
        IReadOnlyList<string> tokens,
        string modality,
        IDictionary<string, IReadOnlyList<string>> artifactMap)
    {
        if (tokens.Count == 0)
            return tokens;

        var normalizedTokens = tokens.ToList();

        if (string.Equals(modality, "ocr", StringComparison.Ordinal))
        {
            var converted = normalizedTokens
                .Select(ApplyCharacterReplacements)
                .ToArray();
            artifactMap["ocr"] = converted
                .Where((value, index) => !string.Equals(value, normalizedTokens[index], StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            normalizedTokens = converted.ToList();
        }

        if (string.Equals(modality, "voice", StringComparison.Ordinal))
        {
            var converted = normalizedTokens
                .Select(ApplyVoiceReplacement)
                .ToArray();
            artifactMap["voice"] = converted
                .Where((value, index) => !string.Equals(value, normalizedTokens[index], StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            normalizedTokens = converted.ToList();
        }

        return normalizedTokens;
    }

    private IReadOnlyList<string> ApplyMorphology(
        IReadOnlyList<string> tokens,
        IDictionary<string, IReadOnlyList<string>> artifactMap)
    {
        var morphologyApplied = new List<string>();
        var transformed = tokens
            .Select(token =>
            {
                if (_options.MorphologyMap.TryGetValue(token, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
                {
                    morphologyApplied.Add(mapped);
                    return mapped;
                }

                if (token.EndsWith("ies", StringComparison.Ordinal) && token.Length > 3)
                {
                    var singular = token[..^3] + "y";
                    morphologyApplied.Add(singular);
                    return singular;
                }

                if (token.EndsWith('s') && token.Length > 2)
                {
                    var singular = token[..^1];
                    morphologyApplied.Add(singular);
                    return singular;
                }

                return token;
            })
            .ToArray();

        artifactMap["morphology"] = morphologyApplied
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return transformed;
    }

    private IReadOnlyList<string> ApplyStopwordFilter(
        IReadOnlyList<string> tokens,
        IDictionary<string, IReadOnlyList<string>> artifactMap)
    {
        if (tokens.Count == 0 || _options.StopwordSet.Count == 0)
        {
            artifactMap["stopword"] = [];
            return tokens;
        }

        var removed = new List<string>();
        var filtered = new List<string>(tokens.Count);
        foreach (var token in tokens)
        {
            if (_options.StopwordSet.Contains(token))
            {
                removed.Add(token);
                continue;
            }

            filtered.Add(token);
        }

        artifactMap["stopword"] = removed
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return filtered;
    }

    private IReadOnlyList<string> ExpandAliasesAndSynonyms(
        IReadOnlyList<string> tokens,
        IDictionary<string, IReadOnlyList<string>> artifactMap)
    {
        var expanded = new List<string>(tokens);
        var aliasApplied = new List<string>();
        var synonymApplied = new List<string>();

        foreach (var token in tokens)
        {
            if (_options.AliasMap.TryGetValue(token, out var aliasValues))
            {
                foreach (var alias in aliasValues)
                {
                    var normalizedAlias = NormalizeCore(alias);
                    if (string.IsNullOrWhiteSpace(normalizedAlias))
                        continue;

                    expanded.Add(normalizedAlias);
                    aliasApplied.Add(normalizedAlias);
                }
            }

            if (_options.SynonymMap.TryGetValue(token, out var synonymValues))
            {
                foreach (var synonym in synonymValues)
                {
                    var normalizedSynonym = NormalizeCore(synonym);
                    if (string.IsNullOrWhiteSpace(normalizedSynonym))
                        continue;

                    expanded.Add(normalizedSynonym);
                    synonymApplied.Add(normalizedSynonym);
                }
            }
        }

        artifactMap["alias"] = aliasApplied
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        artifactMap["synonym"] = synonymApplied
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return expanded;
    }

    private string ApplyCharacterReplacements(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return token;

        var builder = new StringBuilder(token.Length);
        foreach (var character in token)
        {
            var replacement = character.ToString(CultureInfo.InvariantCulture);
            if (_options.OcrReplacementMap.TryGetValue(replacement, out var mapped))
            {
                builder.Append(mapped);
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private string ApplyVoiceReplacement(string token)
    {
        if (_options.VoiceReplacementMap.TryGetValue(token, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
            return mapped;

        return token;
    }

    private string NormalizeCore(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (_options.EnableAccentFolding)
            normalized = RemoveDiacritics(normalized);

        normalized = NonWordPattern.Replace(normalized, " ");
        normalized = WhitespacePattern.Replace(normalized, " ").Trim();
        return normalized;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static IReadOnlyList<string> Tokenize(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
            return [];

        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    private static string[] DistinctPreservingOrder(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return [];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>(values.Count);
        foreach (var value in values)
        {
            if (!seen.Add(value))
                continue;

            ordered.Add(value);
        }

        return ordered.ToArray();
    }

    private static string DetectModality(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Contains("ocr", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("scan", StringComparison.OrdinalIgnoreCase)
            || trimmed.Any(character => char.IsDigit(character)))
        {
            return "ocr";
        }

        if (trimmed.Contains("voice", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("spoken", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("gonna", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("wanna", StringComparison.OrdinalIgnoreCase))
        {
            return "voice";
        }

        return "text";
    }
}
