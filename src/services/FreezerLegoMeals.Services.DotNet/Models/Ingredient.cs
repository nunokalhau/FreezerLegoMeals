using System.Collections.Generic;

namespace FreezerLegoMeals.Services.DotNet.Models
{
    public class Ingredient
    {
        public int Id { get; set; }
        public string Name { get; set; }
        
        // Navigation properties
        public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
    }
}