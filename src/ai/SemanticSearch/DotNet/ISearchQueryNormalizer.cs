namespace SemanticSearch.DotNet;

public interface ISearchQueryNormalizer
{
    SearchNormalizationResult Normalize(string query);
}
