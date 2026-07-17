using System.ComponentModel.DataAnnotations;

namespace FreezerLegoMeals.Repository.DotNet.Entities;

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
    
    // Navigation properties
    public ICollection<RecipeIngredientEntity> RecipeIngredients { get; set; } = new List<RecipeIngredientEntity>();
}