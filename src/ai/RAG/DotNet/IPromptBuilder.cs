namespace RAG.DotNet;

using Domain.DotNet;

public interface IPromptBuilder
{
    string Build(
        string question,
        IReadOnlyList<RetrievalRecipe> recipes,
        string? intentType,
        LocalizationOptions localizationOptions,
        string? requestedLanguage = null);
}