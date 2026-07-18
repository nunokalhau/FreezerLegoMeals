using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Repository.DotNet.Entities;

/// <summary>
/// Entity class representing a recipe in the database.
/// </summary>
public class RecipeEntity
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string SourcePath { get; set; } = string.Empty;
    
    public string Tags { get; set; } = string.Empty;
    
    public int? Servings { get; set; }
    
    public int? TimeToPrepare { get; set; }
    
    public string Prepping { get; set; } = string.Empty;
    
    public string FreezingNotes { get; set; } = string.Empty;
    
    public string ReheatNotes { get; set; } = string.Empty;
    
    public string Combinations { get; set; } = string.Empty;
    
    public string Notes { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<RecipeIngredientEntity> RecipeIngredients { get; set; } = new List<RecipeIngredientEntity>();
    public ICollection<RecipeCombinationItemEntity> RecipeCombinationItems { get; set; } = new List<RecipeCombinationItemEntity>();
}