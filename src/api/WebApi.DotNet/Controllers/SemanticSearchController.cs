using Microsoft.AspNetCore.Mvc;
using SemanticSearch.DotNet;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Contracts.Responses;

namespace WebApi.DotNet.Controllers;

[ApiController]
[Route("api/semantic-search")]
public sealed class SemanticSearchController : ControllerBase
{
    private readonly SemanticSearchService _semanticSearchService;

    public SemanticSearchController(SemanticSearchService semanticSearchService)
    {
        _semanticSearchService = semanticSearchService ?? throw new ArgumentNullException(nameof(semanticSearchService));
    }

    [HttpPost]
    public async Task<ActionResult<IReadOnlyList<SemanticSearchResponse>>> Search(
        [FromBody] SemanticSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Query is required");

        var results = await _semanticSearchService.SearchAsync(request.Query, request.TopK, cancellationToken);
        return Ok(results.Select(result => new SemanticSearchResponse
        {
            RecipeId = result.RecipeId,
            Title = result.Title,
            Score = result.Score,
            MatchedText = result.MatchedText,
            Reason = result.Reason
        }).ToList());
    }
}