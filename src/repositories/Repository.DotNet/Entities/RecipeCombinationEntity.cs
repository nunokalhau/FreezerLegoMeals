using System.ComponentModel.DataAnnotations;

namespace Repository.DotNet.Entities;

/// <summary>
/// Entity class representing a recipe combination in the database.
/// </summary>
public class RecipeCombinationEntity
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }

    public string? Notes { get; set; }
    
    // Navigation properties
    public ICollection<RecipeCombinationItemEntity> RecipeCombinationItems { get; set; } = new List<RecipeCombinationItemEntity>();
    public ICollection<RecipeCombinationTranslationEntity> Translations { get; set; } = new List<RecipeCombinationTranslationEntity>();
}