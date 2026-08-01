using System.ComponentModel.DataAnnotations;

namespace Repository.DotNet.Entities;

/// <summary>
/// Entity class representing an ingredient in the database.
/// </summary>
public class IngredientEntity
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    public string? Unit { get; set; }
    
    // Navigation properties
    public ICollection<RecipeIngredientEntity> RecipeIngredients { get; set; } = new List<RecipeIngredientEntity>();
    public ICollection<IngredientTranslationEntity> Translations { get; set; } = new List<IngredientTranslationEntity>();
}