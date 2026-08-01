using System.ComponentModel.DataAnnotations;

namespace Repository.DotNet.Entities;

/// <summary>
/// Entity class representing an item in a recipe combination.
/// </summary>
public class RecipeCombinationItemEntity
{
    public int CombinationId { get; set; }
    
    public int RecipeId { get; set; }
    
    public int Position { get; set; }

    public string? Notes { get; set; }
    
    // Navigation properties
    public RecipeCombinationEntity RecipeCombination { get; set; } = null!;
    public RecipeEntity Recipe { get; set; } = null!;
}