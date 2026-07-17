namespace FreezerLegoMeals.Services.DotNet.Models
{
    public class RecipeIngredient
    {
        public int RecipeId { get; set; }
        public int IngredientId { get; set; }
        public double? Amount { get; set; }
        public string Unit { get; set; }
        
        // Navigation properties
        public Recipe Recipe { get; set; }
        public Ingredient Ingredient { get; set; }
    }
}