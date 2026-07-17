using System.Collections.Generic;

namespace FreezerLegoMeals.Domain.DotNet;

public class RecipeCombination
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    
    // Navigation properties for domain objects
    public ICollection<RecipeCombinationItem> RecipeCombinationItems { get; set; } = new List<RecipeCombinationItem>();
}