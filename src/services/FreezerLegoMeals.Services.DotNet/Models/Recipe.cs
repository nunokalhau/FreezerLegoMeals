using System.Collections.Generic;

namespace FreezerLegoMeals.Services.DotNet.Models
{
    public class Recipe
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string SourcePath { get; set; }
        public string Tags { get; set; }
        public int? Servings { get; set; }
        public int? TimeToPrepare { get; set; }
        public string Prepping { get; set; }
        public string FreezingNotes { get; set; }
        public string ReheatNotes { get; set; }
        public string Combinations { get; set; }
        public string Notes { get; set; }
        
        // Navigation properties
        public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
        public ICollection<RecipeCombinationItem> RecipeCombinationItems { get; set; } = new List<RecipeCombinationItem>();
    }
}