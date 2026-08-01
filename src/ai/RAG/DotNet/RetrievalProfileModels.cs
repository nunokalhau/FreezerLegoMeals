namespace RAG.DotNet;

public enum RetrievalProfileFamily
{
    CanonicalMultilingualProjection,
    PerLanguageProjection,
    HybridPrecisionRecall
}

public sealed record RetrievalProfileDescriptor(
    string ProfileId,
    RetrievalProfileFamily ProfileFamily,
    string SelectionReason,
    string ContractVersion = "retrieval-contract-v1");

public sealed record RetrievalRankingEntry(string RecipeId, double Score);
