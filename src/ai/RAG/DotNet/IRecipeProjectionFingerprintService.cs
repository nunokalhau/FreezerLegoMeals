namespace RAG.DotNet;

public interface IRecipeProjectionFingerprintService
{
    string Compute(RecipeProjectionFingerprintInput input);
}
