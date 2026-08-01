using Domain.DotNet;

namespace RAG.DotNet;

public interface IRetrievalProfileSelector
{
    RetrievalProfileDescriptor Select(string question, LocalizationOptions localizationOptions);
}

public sealed class DefaultRetrievalProfileSelector : IRetrievalProfileSelector
{
    public RetrievalProfileDescriptor Select(string question, LocalizationOptions localizationOptions)
    {
        ArgumentNullException.ThrowIfNull(localizationOptions);

        if (localizationOptions.StrictMode)
        {
            return new RetrievalProfileDescriptor(
                ProfileId: "per-language-projection",
                ProfileFamily: RetrievalProfileFamily.PerLanguageProjection,
                SelectionReason: "strict-localization");
        }

        if (!string.Equals(localizationOptions.PreferredLanguage, "en", StringComparison.OrdinalIgnoreCase))
        {
            return new RetrievalProfileDescriptor(
                ProfileId: "hybrid-precision-recall",
                ProfileFamily: RetrievalProfileFamily.HybridPrecisionRecall,
                SelectionReason: "non-default-language");
        }

        return new RetrievalProfileDescriptor(
            ProfileId: "canonical-multilingual-projection",
            ProfileFamily: RetrievalProfileFamily.CanonicalMultilingualProjection,
            SelectionReason: "default-policy");
    }
}
