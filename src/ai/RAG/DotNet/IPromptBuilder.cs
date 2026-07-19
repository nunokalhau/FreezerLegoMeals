namespace RAG.DotNet;

public interface IPromptBuilder
{
    string Build(string question, IReadOnlyList<RetrievalRecipe> recipes);
}