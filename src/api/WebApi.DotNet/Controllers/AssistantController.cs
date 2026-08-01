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

        var localization = new AssistantLocalizationRequest(
            ExplicitLanguage: request.Language,
            NegotiatedLanguages: ParseNegotiatedLanguages(Request),
            StrictMode: request.StrictMode);

        var response = await _assistantService.ChatAsync(request.Message, request.ConversationId, localization, cancellationToken);

        return Ok(new AssistantChatResponse
        {
            ConversationId = response.ConversationId,
            Response = response
                .Response
        });
    }

    private static IReadOnlyList<string> ParseNegotiatedLanguages(HttpRequest request)
    {
        var headerValue = request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return Array.Empty<string>();
        }

        return headerValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Split(';', StringSplitOptions.RemoveEmptyEntries)[0].Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}