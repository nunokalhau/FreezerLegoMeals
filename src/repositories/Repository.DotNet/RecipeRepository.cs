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
    Task<RecipeCombination?> GetCombinationByIdAsync(int id);
    Task<IEnumerable<Ingredient>> GetIngredientsAsync();
    Task<Ingredient?> GetIngredientByNameAsync(string name);
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
        
        var ingredientNames = ingredients
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim().ToLower())
            .Distinct()
            .ToList();
        if (!ingredientNames.Any()) return Enumerable.Empty<Recipe>();

        // Match ingredient terms as partial text so queries like "tofu" match "tofu firme".
        IQueryable<IngredientEntity> matchingIngredients = _context.Ingredients.Where(_ => false);
        foreach (var ingredientTerm in ingredientNames)
        {
            var currentTerm = ingredientTerm;
            matchingIngredients = matchingIngredients.Union(
                _context.Ingredients.Where(i => i.Name.ToLower().Contains(currentTerm)));
        }

        var matchingIngredientIds = await matchingIngredients
            .Select(i => i.Id)
            .Distinct()
            .ToListAsync();

        if (!matchingIngredientIds.Any())
        {
            return Enumerable.Empty<Recipe>();
        }

        var recipeIds = await _context.RecipeIngredients
            .Where(ri => matchingIngredientIds.Contains(ri.IngredientId))
            .Select(ri => ri.RecipeId)
            .Distinct()
            .ToListAsync();

        if (!recipeIds.Any())
        {
            return Enumerable.Empty<Recipe>();
        }

        var recipeRows = await _context.RecipeIngredients
            .AsNoTracking()
            .Where(ri => recipeIds.Contains(ri.RecipeId))
            .Select(ri => new
            {
                ri.RecipeId,
                RecipeName = ri.Recipe.Name,
                RecipeSourcePath = ri.Recipe.SourcePath,
                RecipeTags = ri.Recipe.Tags,
                RecipeServings = ri.Recipe.Servings,
                RecipeTimeToPrepare = ri.Recipe.TimeToPrepare,
                RecipePrepping = ri.Recipe.Prepping,
                RecipeFreezingNotes = ri.Recipe.FreezingNotes,
                RecipeReheatNotes = ri.Recipe.ReheatNotes,
                RecipeCombinations = ri.Recipe.Combinations,
                RecipeNotes = ri.Recipe.Notes,
                ri.IngredientId,
                ri.Amount,
                ri.Unit,
                IngredientName = ri.Ingredient.Name
            })
            .ToListAsync();

        var recipes = recipeRows
            .GroupBy(r => new
            {
                r.RecipeId,
                r.RecipeName,
                r.RecipeSourcePath,
                r.RecipeTags,
                r.RecipeServings,
                r.RecipeTimeToPrepare,
                r.RecipePrepping,
                r.RecipeFreezingNotes,
                r.RecipeReheatNotes,
                r.RecipeCombinations,
                r.RecipeNotes
            })
            .Select(group =>
            {
                var recipe = new Recipe
                {
                    Id = group.Key.RecipeId,
                    Name = group.Key.RecipeName,
                    SourcePath = group.Key.RecipeSourcePath,
                    Tags = group.Key.RecipeTags ?? string.Empty,
                    Servings = group.Key.RecipeServings,
                    TimeToPrepare = group.Key.RecipeTimeToPrepare,
                    Prepping = group.Key.RecipePrepping ?? string.Empty,
                    FreezingNotes = group.Key.RecipeFreezingNotes ?? string.Empty,
                    ReheatNotes = group.Key.RecipeReheatNotes ?? string.Empty,
                    Combinations = group.Key.RecipeCombinations ?? string.Empty,
                    Notes = group.Key.RecipeNotes ?? string.Empty,
                    RecipeIngredients = new List<RecipeIngredient>(),
                    RecipeCombinationItems = new List<RecipeCombinationItem>()
                };

                foreach (var row in group)
                {
                    recipe.RecipeIngredients.Add(new RecipeIngredient
                    {
                        RecipeId = row.RecipeId,
                        IngredientId = row.IngredientId,
                        Amount = row.Amount,
                        Unit = row.Unit,
                        Recipe = recipe,
                        Ingredient = new Ingredient
                        {
                            Id = row.IngredientId,
                            Name = row.IngredientName,
                            RecipeIngredients = new List<RecipeIngredient>()
                        }
                    });
                }

                return recipe;
            })
            .OrderBy(r => r.Id)
            .ToList();

        return recipes;
    }

    public async Task<IEnumerable<RecipeCombination>> GetCombinationsAsync()
    {
        var entities = await _context.RecipeCombinations
            .Include(rc => rc.RecipeCombinationItems)
                .ThenInclude(rci => rci.Recipe)
            .ToListAsync();

        return entities.Select(e => MapRecipeCombination(e));
    }

    public async Task<RecipeCombination?> GetCombinationByIdAsync(int id)
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

    public async Task<Ingredient?> GetIngredientByNameAsync(string name)
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
            Tags = entity.Tags ?? string.Empty,
            Servings = entity.Servings,
            TimeToPrepare = entity.TimeToPrepare,
            Prepping = entity.Prepping ?? string.Empty,
            FreezingNotes = entity.FreezingNotes ?? string.Empty,
            ReheatNotes = entity.ReheatNotes ?? string.Empty,
            Combinations = entity.Combinations ?? string.Empty,
            Notes = entity.Notes ?? string.Empty,
            RecipeIngredients = new List<RecipeIngredient>(),
            RecipeCombinationItems = new List<RecipeCombinationItem>()
        };

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

        if (entity.RecipeCombinationItems != null)
        {
            foreach (var combinationItem in entity.RecipeCombinationItems)
            {
                var domainCombinationItem = new RecipeCombinationItem
                {
                    Id = 0,
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
                    Recipe = null,
                    Ingredient = ingredient
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
            Description = entity.Description ?? string.Empty,
            RecipeCombinationItems = new List<RecipeCombinationItem>()
        };

        if (entity.RecipeCombinationItems != null)
        {
            foreach (var combinationItem in entity.RecipeCombinationItems)
            {
                var domainCombinationItem = new RecipeCombinationItem
                {
                    Id = 0,
                    CombinationId = combinationItem.CombinationId,
                    RecipeId = combinationItem.RecipeId,
                    Position = combinationItem.Position,
                    RecipeCombination = combination,
                    Recipe = null
                };
                
                combination.RecipeCombinationItems.Add(domainCombinationItem);
            }
        }

        return combination;
    }
}