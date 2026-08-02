using Domain.DotNet;

namespace RAG.DotNet;

public sealed record RetrievalIntentClassification(
    string Intent,
    double Confidence = 1.0,
    string? MatchedRule = null,
    string? DetectedLanguage = null);

public sealed record RetrievalRequestContext(
    string OriginalQuestion,
    RetrievalIntentClassification IntentClassification,
    LocalizationOptions LocalizationOptions,
    bool StrictMode);