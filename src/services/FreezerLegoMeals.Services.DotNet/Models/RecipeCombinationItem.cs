namespace FreezerLegoMeals.Services.DotNet.Models
{
    public class RecipeCombinationItem
    {
        public int Id { get; set; }
        public int CombinationId { get; set; }
        public int RecipeId { get; set; }
        public int Position { get; set; }
        
        // Navigation properties
        public RecipeCombination RecipeCombination { get; set; }
        public Recipe Recipe { get; set; }
    }
}