using Microsoft.AspNetCore.Mvc;
using Services.DotNet;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Contracts.Responses;
using WebApi.DotNet.Services;

namespace WebApi.DotNet.Controllers;

/// <summary>
/// Controller for recipe-related operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RecipesController : ControllerBase
{
    private readonly IMealService _mealService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecipesController"/> class.
    /// </summary>
    /// <param name="mealService">The meal service to use for business logic.</param>
    public RecipesController(IMealService mealService)
    {
        _mealService = mealService ?? throw new ArgumentNullException(nameof(mealService));
    }

    /// <summary>
    /// Search for recipes containing any of the specified ingredients.
    /// </summary>
    /// <param name="request">The search request containing ingredients.</param>
    /// <returns>List of matching recipes.</returns>
    [HttpPost("search")]
    public async Task<ActionResult<SearchRecipesResponse>> SearchRecipesByIngredients([FromBody] SearchRecipesRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        var ingredients = request.Ingredients;
        if (ingredients is null || !ingredients.Any())
            return BadRequest("At least one ingredient is required");

        var recipes = await _mealService.SearchRecipesByIngredientsAsync(ingredients);
        var mappedRecipes = recipes.Select(MapSearchRecipe).ToList();
        
        var response = new SearchRecipesResponse
        {
            Recipes = mappedRecipes,
            TotalRecipesFound = mappedRecipes.Count
        };

        return Ok(response);
    }

    /// <summary>
    /// Get a specific recipe by ID.
    /// </summary>
    /// <param name="id">The recipe ID.</param>
    /// <returns>The recipe if found, null otherwise.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<GetRecipeByIdResponse>> GetRecipeById([FromRoute] int id)
    {
        if (id <= 0)
            return BadRequest("Recipe ID is required");

        var recipe = await _mealService.GetRecipeByIdAsync(id);
        
        if (recipe == null)
            return NotFound("Recipe not found");

        var response = new GetRecipeByIdResponse
        {
            Recipe = recipe
        };

        return Ok(response);
    }

    /// <summary>
    /// Get a localized recipe by canonical recipe ID.
    /// </summary>
    /// <param name="id">Canonical recipe ID.</param>
    /// <param name="request">Localization request query options.</param>
    /// <param name="localizationService">API localization adapter service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Localized recipe payload with localization metadata.</returns>
    [HttpGet("{id:int}/localized")]
    public async Task<ActionResult<GetLocalizedRecipeByIdResponse>> GetLocalizedRecipeById(
        [FromRoute] int id,
        [FromQuery] LocalizedRecipeQueryRequest request,
        [FromServices] IApiRecipeLocalizationService localizationService,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest("Recipe ID is required");

        if (localizationService is null)
            return StatusCode(StatusCodes.Status500InternalServerError, "Localization service is required");

        var response = await localizationService.GetLocalizedRecipeByIdAsync(id, request, Request, cancellationToken);
        if (response is null)
        {
            return NotFound("Localized recipe not found");
        }

        return Ok(response);
    }

    /// <summary>
    /// Search for recipes containing specified ingredients and return detailed information.
    /// </summary>
    /// <param name="request">The search request with a natural language query.</param>
    /// <returns>Detailed search results.</returns>
    [HttpPost("find-by-ingredients")]
    public async Task<ActionResult<FindMealsWithIngredientsResponse>> FindMealsWithIngredients([FromBody] FindMealsWithIngredientsRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Query is required");

        var result = await _mealService.FindMealsWithIngredientsAsync(request.Query);
        var mappedRecipes = (result.Recipes ?? Enumerable.Empty<Domain.DotNet.Recipe>())
            .Select(MapSearchRecipe)
            .ToList();

        var response = new FindMealsWithIngredientsResponse
        {
            Query = result.Query ?? request.Query,
            TotalRecipesFound = mappedRecipes.Count,
            SearchTerms = result.SearchTerms ?? Enumerable.Empty<string>(),
            Recipes = mappedRecipes,
            Message = result.Message ?? string.Empty
        };

        return Ok(response);
    }

    private static RecipeSearchItemResponse MapSearchRecipe(Domain.DotNet.Recipe recipe)
    {
        return new RecipeSearchItemResponse
        {
            Id = recipe.Id,
            Name = recipe.Name,
            SourcePath = recipe.SourcePath,
            Tags = recipe.Tags,
            Servings = recipe.Servings,
            TimeToPrepare = recipe.TimeToPrepare,
            Prepping = recipe.Prepping,
            FreezingNotes = recipe.FreezingNotes,
            ReheatNotes = recipe.ReheatNotes,
            Combinations = recipe.Combinations,
            Notes = recipe.Notes,
            RecipeIngredients = (recipe.RecipeIngredients ?? Enumerable.Empty<Domain.DotNet.RecipeIngredient>())
                .Select(ri => new RecipeSearchIngredientResponse
                {
                    IngredientId = ri.IngredientId,
                    Amount = ri.Amount,
                    Unit = ri.Unit,
                    Name = ri.Ingredient?.Name ?? string.Empty
                })
                .ToList()
        };
    }

    /// <summary>
    /// Get detailed information about a specific recipe.
    /// </summary>
    /// <param name="id">The recipe ID.</param>
    /// <returns>Detailed recipe information.</returns>
    [HttpGet("{id:int}/details")]
    public async Task<ActionResult<GetRecipeDetailsResponse>> GetRecipeDetails([FromRoute] int id)
    {
        if (id <= 0)
            return BadRequest("Recipe ID is required");

        var result = await _mealService.GetRecipeDetailsAsync(id);

        if (result.Recipe == null)
            return NotFound("Recipe details not found");

        var response = new GetRecipeDetailsResponse
        {
            Recipe = result.Recipe,
            Message = result.Message ?? string.Empty
        };

        return Ok(response);
    }
}