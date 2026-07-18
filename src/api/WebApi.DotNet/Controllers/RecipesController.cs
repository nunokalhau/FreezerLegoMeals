using Microsoft.AspNetCore.Mvc;
using Services.DotNet;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Contracts.Responses;
using Domain.DotNet;

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

        if (!request.Ingredients?.Any() ?? true)
            return BadRequest("At least one ingredient is required");

        var recipes = await _mealService.SearchRecipesByIngredientsAsync(request.Ingredients);
        
        var response = new SearchRecipesResponse
        {
            Recipes = recipes,
            TotalRecipesFound = recipes?.Count() ?? 0
        };

        return Ok(response);
    }

    /// <summary>
    /// Get a specific recipe by ID.
    /// </summary>
    /// <param name="request">The request containing the recipe ID.</param>
    /// <returns>The recipe if found, null otherwise.</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<GetRecipeByIdResponse>> GetRecipeById([FromRoute] GetRecipeByIdRequest request)
    {
        if (request == null || request.Id <= 0)
            return BadRequest("Recipe ID is required");

        var recipe = await _mealService.GetRecipeByIdAsync(request.Id);
        
        if (recipe == null)
            return NotFound("Recipe not found");

        var response = new GetRecipeByIdResponse
        {
            Recipe = recipe
        };

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

        var response = new FindMealsWithIngredientsResponse
        {
            Query = result.Query ?? request.Query,
            TotalRecipesFound = result.TotalRecipesFound,
            SearchTerms = result.SearchTerms ?? Enumerable.Empty<string>(),
            Recipes = result.Recipes ?? Enumerable.Empty<Recipe>(),
            Message = result.Message ?? string.Empty
        };

        return Ok(response);
    }

    /// <summary>
    /// Get detailed information about a specific recipe.
    /// </summary>
    /// <param name="request">The request containing the recipe ID.</param>
    /// <returns>Detailed recipe information.</returns>
    [HttpGet("{id}/details")]
    public async Task<ActionResult<GetRecipeDetailsResponse>> GetRecipeDetails([FromRoute] GetRecipeByIdRequest request)
    {
        if (request == null || request.Id <= 0)
            return BadRequest("Recipe ID is required");

        var result = await _mealService.GetRecipeDetailsAsync(request.Id);

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