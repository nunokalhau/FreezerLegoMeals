using System.ComponentModel.DataAnnotations;

namespace Repository.DotNet.Entities;

/// <summary>
/// Entity class representing the many-to-many relationship between recipes and ingredients.
/// </summary>
public class RecipeIngredientEntity
{
    public int RecipeId { get; set; }
    
    public int IngredientId { get; set; }
    
    public double? Amount { get; set; }
    
    public string? AmountText { get; set; }

    public string? Unit { get; set; }

    public string? Notes { get; set; }

    public string? SourceText { get; set; }
    
    // Navigation properties
    public RecipeEntity Recipe { get; set; } = null!;
    public IngredientEntity Ingredient { get; set; } = null!;
    public ICollection<RecipeIngredientLocalizationEntity> Localizations { get; set; } = new List<RecipeIngredientLocalizationEntity>();
}