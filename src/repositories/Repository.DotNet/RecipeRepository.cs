using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Domain.DotNet;
using Repository.DotNet.Entities;

namespace Repository.DotNet;

/// <summary>
/// Interface for repository operations in the .NET service layer.
/// </summary>
public interface IRecipeRepository
{
    Task<IEnumerable<Recipe>> GetRecipesAsync();
    Task<Recipe?> GetRecipeByIdAsync(int id);
    Task<IEnumerable<Recipe>> FindRecipesWithIngredientsAsync(IEnumerable<string> ingredients);
    Task<IEnumerable<RecipeCombination>> GetCombinationsAsync();
    Task<RecipeCombination> GetCombinationByIdAsync(int id);
    Task<IEnumerable<Ingredient>> GetIngredientsAsync();
    Task<Ingredient> GetIngredientByNameAsync(string name);
}

/// <summary>
/// Implementation of the recipe repository for .NET, using EF Core to map entities to domain models.
/// </summary>
public class RecipeRepository : IRecipeRepository
{
    private readonly FreezerLegoMealsContext _context;

    public RecipeRepository(FreezerLegoMealsContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<Recipe>> GetRecipesAsync()
    {
        var entities = await _context.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .Include(r => r.RecipeCombinationItems)
                .ThenInclude(rci => rci.RecipeCombination)
            .ToListAsync();

        return entities.Select(e => MapRecipe(e));
    }

    public async Task<Recipe?> GetRecipeByIdAsync(int id)
    {
        var entity = await _context.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .Include(r => r.RecipeCombinationItems)
                .ThenInclude(rci => rci.RecipeCombination)
            .FirstOrDefaultAsync(r => r.Id == id);

        return entity != null ? MapRecipe(entity) : null;
    }

    public async Task<IEnumerable<Recipe>> FindRecipesWithIngredientsAsync(IEnumerable<string> ingredients)
    {
        if (ingredients == null) throw new ArgumentNullException(nameof(ingredients));
        
        var ingredientNames = ingredients.Where(i => !string.IsNullOrEmpty(i)).ToList();
        if (!ingredientNames.Any()) return Enumerable.Empty<Recipe>();

        // Get recipe IDs for recipes containing any of the specified ingredients
        var recipeIds = await _context.RecipeIngredients
            .Where(ri => ingredientNames.Contains(ri.Ingredient.Name))
            .Select(ri => ri.RecipeId)
            .Distinct()
            .ToListAsync();

        // Load the actual recipes with their ingredients
        var entities = await _context.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .Where(r => recipeIds.Contains(r.Id))
            .ToListAsync();

        return entities.Select(e => MapRecipe(e));
    }

    public async Task<IEnumerable<RecipeCombination>> GetCombinationsAsync()
    {
        var entities = await _context.RecipeCombinations
            .Include(rc => rc.RecipeCombinationItems)
                .ThenInclude(rci => rci.Recipe)
            .ToListAsync();

        return entities.Select(e => MapRecipeCombination(e));
    }

    public async Task<RecipeCombination> GetCombinationByIdAsync(int id)
    {
        var entity = await _context.RecipeCombinations
            .Include(rc => rc.RecipeCombinationItems)
                .ThenInclude(rci => rci.Recipe)
            .FirstOrDefaultAsync(rc => rc.Id == id);

        return entity != null ? MapRecipeCombination(entity) : null;
    }

    public async Task<IEnumerable<Ingredient>> GetIngredientsAsync()
    {
        var entities = await _context.Ingredients
            .ToListAsync();

        return entities.Select(e => MapIngredient(e));
    }

    public async Task<Ingredient> GetIngredientByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        
        var entity = await _context.Ingredients
            .FirstOrDefaultAsync(i => i.Name == name);

        return entity != null ? MapIngredient(entity) : null;
    }

    private Recipe MapRecipe(RecipeEntity entity)
    {
        if (entity == null) return null;

        var recipe = new Recipe
        {
            Id = entity.Id,
            Name = entity.Name,
            SourcePath = entity.SourcePath,
            Tags = entity.Tags,
            Servings = entity.Servings,
            TimeToPrepare = entity.TimeToPrepare,
            Prepping = entity.Prepping,
            FreezingNotes = entity.FreezingNotes,
            ReheatNotes = entity.ReheatNotes,
            Combinations = entity.Combinations,
            Notes = entity.Notes,
            RecipeIngredients = new List<RecipeIngredient>(),
            RecipeCombinationItems = new List<RecipeCombinationItem>()
        };

        // Map recipe ingredients
        if (entity.RecipeIngredients != null)
        {
            foreach (var recipeIngredient in entity.RecipeIngredients)
            {
                var domainIngredient = new RecipeIngredient
                {
                    RecipeId = recipeIngredient.RecipeId,
                    IngredientId = recipeIngredient.IngredientId,
                    Amount = recipeIngredient.Amount,
                    Unit = recipeIngredient.Unit,
                    Recipe = recipe, // Set navigation property
                    Ingredient = MapIngredient(recipeIngredient.Ingredient) // Map ingredient
                };
                
                recipe.RecipeIngredients.Add(domainIngredient);
            }
        }

        // Map combination items
        if (entity.RecipeCombinationItems != null)
        {
            foreach (var combinationItem in entity.RecipeCombinationItems)
            {
                var domainCombinationItem = new RecipeCombinationItem
                {
                    Id = combinationItem.Id,
                    CombinationId = combinationItem.CombinationId,
                    RecipeId = combinationItem.RecipeId,
                    Position = combinationItem.Position,
                    RecipeCombination = MapRecipeCombination(combinationItem.RecipeCombination), // Map combination
                    Recipe = recipe // Set navigation property
                };
                
                recipe.RecipeCombinationItems.Add(domainCombinationItem);
            }
        }

        return recipe;
    }

    private Ingredient MapIngredient(IngredientEntity entity)
    {
        if (entity == null) return null;

        var ingredient = new Ingredient
        {
            Id = entity.Id,
            Name = entity.Name,
            RecipeIngredients = new List<RecipeIngredient>()
        };

        // Map recipe ingredients that reference this ingredient
        if (entity.RecipeIngredients != null)
        {
            foreach (var recipeIngredient in entity.RecipeIngredients)
            {
                var domainRecipeIngredient = new RecipeIngredient
                {
                    RecipeId = recipeIngredient.RecipeId,
                    IngredientId = recipeIngredient.IngredientId,
                    Amount = recipeIngredient.Amount,
                    Unit = recipeIngredient.Unit,
                    Recipe = null, // Will be set when mapping the recipe
                    Ingredient = ingredient // Set navigation property
                };
                
                ingredient.RecipeIngredients.Add(domainRecipeIngredient);
            }
        }

        return ingredient;
    }

    private RecipeCombination MapRecipeCombination(RecipeCombinationEntity entity)
    {
        if (entity == null) return null;

        var combination = new RecipeCombination
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            RecipeCombinationItems = new List<RecipeCombinationItem>()
        };

        // Map combination items
        if (entity.RecipeCombinationItems != null)
        {
            foreach (var combinationItem in entity.RecipeCombinationItems)
            {
                var domainCombinationItem = new RecipeCombinationItem
                {
                    Id = combinationItem.Id,
                    CombinationId = combinationItem.CombinationId,
                    RecipeId = combinationItem.RecipeId,
                    Position = combinationItem.Position,
                    RecipeCombination = combination, // Set navigation property
                    Recipe = null // Will be set when mapping the recipe
                };
                
                combination.RecipeCombinationItems.Add(domainCombinationItem);
            }
        }

        return combination;
    }
}