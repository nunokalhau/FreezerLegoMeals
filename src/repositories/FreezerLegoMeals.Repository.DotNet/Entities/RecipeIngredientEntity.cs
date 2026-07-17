using System.ComponentModel.DataAnnotations;

namespace FreezerLegoMeals.Repository.DotNet.Entities;

/// <summary>
/// Entity class representing the many-to-many relationship between recipes and ingredients.
/// </summary>
public class RecipeIngredientEntity
{
    [Key]
    public int Id { get; set; }
    
    public int RecipeId { get; set; }
    
    public int IngredientId { get; set; }
    
    public double? Amount { get; set; }
    
    public string Unit { get; set; } = string.Empty;
    
    // Navigation properties
    public RecipeEntity Recipe { get; set; } = null!;
    public IngredientEntity Ingredient { get; set; } = null!;
}