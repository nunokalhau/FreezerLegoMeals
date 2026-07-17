using System.Collections.Generic;

namespace FreezerLegoMeals.Domain.DotNet;

public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    // Navigation properties for domain objects
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}
