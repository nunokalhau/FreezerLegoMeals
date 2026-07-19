using Microsoft.AspNetCore.Mvc;
using Services.DotNet;
using WebApi.DotNet.Contracts.Requests;
using WebApi.DotNet.Contracts.Responses;

namespace WebApi.DotNet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssistantController : ControllerBase
{
    private readonly IAssistantService _assistantService;

    public AssistantController(IAssistantService assistantService)
    {
        _assistantService = assistantService ?? throw new ArgumentNullException(nameof(assistantService));
    }

    [HttpPost("chat")]
    public async Task<ActionResult<AssistantChatResponse>> Chat([FromBody] AssistantChatRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest("Request body is required");

        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message is required");

        var response = await _assistantService.ChatAsync(request.Message, cancellationToken);

        return Ok(new AssistantChatResponse
        {
            Response = response
        });
    }
}