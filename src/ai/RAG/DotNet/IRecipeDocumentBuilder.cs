using Domain.DotNet;

namespace RAG.DotNet;

public interface IRecipeDocumentBuilder
{
    string Build(Recipe recipe);
}
