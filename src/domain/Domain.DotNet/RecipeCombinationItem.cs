namespace Domain.DotNet;

public class RecipeCombinationItem
{
    public int Id { get; set; }
    public int CombinationId { get; set; }
    public int RecipeId { get; set; }
    public int Position { get; set; }
    
    // Navigation properties for domain objects
    public RecipeCombination RecipeCombination { get; set; }
    public Recipe Recipe { get; set; }
}