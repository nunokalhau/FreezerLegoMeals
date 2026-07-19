namespace SemanticSearch.DotNet;

public sealed record SemanticSearchResult(string RecipeId, string Title, double Score, string MatchedText, string Reason);