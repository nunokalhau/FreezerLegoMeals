using Embedding.DotNet;
using Microsoft.AspNetCore.Mvc;
using WebApi.DotNet.Contracts.Requests;

namespace WebApi.DotNet.Controllers;

[ApiController]
[Route("embeddings")]
[Route("api/[controller]")]
public sealed class EmbeddingsController : ControllerBase
{
    private readonly IEmbeddingService _embeddingService;

    public EmbeddingsController(IEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
    }

    [HttpPost]
    public async Task<ActionResult<Contracts.Responses.EmbeddingResponse>> GenerateEmbedding(
        [FromBody] EmbeddingRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Text))
            return BadRequest("Text is required");

        var embedding = await _embeddingService.GenerateEmbeddingAsync(request.Text, cancellationToken);
        return Ok(new Contracts.Responses.EmbeddingResponse
        {
            Model = embedding.Model,
            Dimensions = embedding.Dimensions,
            Embedding = embedding.Embedding
        });
    }
}