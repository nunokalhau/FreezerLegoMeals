using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Services.DotNet;

public sealed class HeuristicAssistantLanguageDetector : IAssistantLanguageDetector
{
    private static readonly Regex TokenRegex = new("[\\p{L}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<string, LanguageProfile> Profiles =
        new Dictionary<string, LanguageProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new([
                "what", "which", "with", "have", "recipes", "recipe", "chicken", "beef", "freezer", "meal", "meals", "cook", "prep", "week"
            ]),
            ["pt"] = new([
                "que", "receitas", "receita", "tens", "tenho", "com", "frango", "carne", "arroz", "congelador", "refeicao", "refeicoes", "cozinhar", "preparar"
            ], ["ã", "õ", "ç"]),
            ["es"] = new([
                "que", "recetas", "receta", "tienes", "con", "pollo", "carne", "arroz", "congelador", "comida", "cocinar", "preparar"
            ], ["ñ"]),
            ["de"] = new([
                "welche", "rezepte", "rezept", "hast", "mit", "huhn", "rind", "reis", "gefrierschrank", "mahlzeit", "kochen", "vorbereiten"
            ], ["ä", "ö", "ü", "ß"]),
            ["fr"] = new([
                "quelles", "recettes", "recette", "avec", "poulet", "boeuf", "riz", "congelateur", "repas", "cuisiner", "preparer"
            ], ["à", "â", "ç", "é", "è", "ê", "ë", "î", "ï", "ô", "ù", "û", "ü", "œ"])
        };

    private readonly HashSet<string> _supportedLanguages;

    public HeuristicAssistantLanguageDetector(IOptions<AssistantLocalizationDefaultsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _supportedLanguages = (options.Value.SupportedLanguages ?? [])
            .Select(NormalizeLanguage)
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Where(language => Profiles.ContainsKey(language))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (_supportedLanguages.Count == 0)
        {
            _supportedLanguages.UnionWith(["en", "pt"]);
        }
    }

    public string? Detect(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var tokens = TokenRegex.Matches(message)
            .Select(match => match.Value.ToLowerInvariant())
            .ToArray();
        if (tokens.Length == 0)
        {
            return null;
        }

        var scores = new List<(string Language, int Score)>();
        foreach (var language in _supportedLanguages)
        {
            var profile = Profiles[language];
            var score = 0;

            foreach (var token in tokens)
            {
                if (profile.Keywords.Contains(token))
                {
                    score += 2;
                }

                if (profile.MarkerFragments.Any(marker => token.Contains(marker, StringComparison.Ordinal)))
                {
                    score += 3;
                }
            }

            scores.Add((language, score));
        }

        var ordered = scores
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Language, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ordered.Length == 0 || ordered[0].Score <= 0)
        {
            return null;
        }

        if (ordered.Length > 1 && ordered[0].Score == ordered[1].Score)
        {
            return null;
        }

        return ordered[0].Language;
    }

    private static string NormalizeLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return string.Empty;
        }

        var trimmed = language.Trim();
        var separatorIndex = trimmed.IndexOf('-');
        if (separatorIndex < 0)
        {
            separatorIndex = trimmed.IndexOf('_');
        }

        return separatorIndex < 0
            ? trimmed.ToLowerInvariant()
            : trimmed[..separatorIndex].ToLowerInvariant();
    }

    private sealed record LanguageProfile(
        IReadOnlySet<string> Keywords,
        IReadOnlyList<string> MarkerFragments)
    {
        public LanguageProfile(IEnumerable<string> keywords)
            : this(new HashSet<string>(keywords, StringComparer.OrdinalIgnoreCase), Array.Empty<string>())
        {
        }

        public LanguageProfile(IEnumerable<string> keywords, IEnumerable<string> markerFragments)
            : this(
                new HashSet<string>(keywords, StringComparer.OrdinalIgnoreCase),
                markerFragments.ToArray())
        {
        }
    }
}