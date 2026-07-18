namespace Domain.DotNet;

public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}
