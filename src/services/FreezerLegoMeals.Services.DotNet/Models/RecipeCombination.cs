using System.Collections.Generic;

namespace FreezerLegoMeals.Services.DotNet.Models
{
    public class RecipeCombination
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        
        // Navigation properties
        public ICollection<RecipeCombinationItem> RecipeCombinationItems { get; set; } = new List<RecipeCombinationItem>();
    }
}