using System.ComponentModel.DataAnnotations;

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
    
    public string? SourcePath { get; set; }
    
    public string? Tags { get; set; }
    
    public int? Servings { get; set; }
    
    public int? TimeToPrepare { get; set; }
    
    public string? Prepping { get; set; }
    
    public string? FreezingNotes { get; set; }
    
    public string? ReheatNotes { get; set; }
    
    public string? Combinations { get; set; }
    
    public string? Notes { get; set; }
    
    // Navigation properties
    public ICollection<RecipeIngredientEntity> RecipeIngredients { get; set; } = new List<RecipeIngredientEntity>();
    public ICollection<RecipeCombinationItemEntity> RecipeCombinationItems { get; set; } = new List<RecipeCombinationItemEntity>();
    public ICollection<RecipeTranslationEntity> Translations { get; set; } = new List<RecipeTranslationEntity>();
    public RecipeIndexMetadataEntity? IndexMetadata { get; set; }
}