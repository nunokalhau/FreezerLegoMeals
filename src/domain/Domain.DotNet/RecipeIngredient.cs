namespace Domain.DotNet;

public class RecipeIngredient
{
    public int RecipeId { get; set; }
    public int IngredientId { get; set; }
    public double? Amount { get; set; }
    public string Unit { get; set; }
    
    public Recipe Recipe { get; set; }
    public Ingredient Ingredient { get; set; }
    
    public RecipeIngredient()
    {
        // Parameterless constructor for serialization
    }
    
    public RecipeIngredient(int recipeId, int ingredientId)
    {
        RecipeId = recipeId;
        IngredientId = ingredientId;
    }
}