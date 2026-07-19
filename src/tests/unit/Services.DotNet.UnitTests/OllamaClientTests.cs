using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Xunit;

namespace Services.DotNet.UnitTests;

public class OllamaClientTests
{
    [Fact]
    public async Task ChatAsync_UsesConfiguredDefaultModelAndReturnsAssistantContent()
    {
        // Arrange
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                message = new
                {
                    role = "assistant",
                    content = "Hello from Ollama"
                }
            })
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        var options = Options.Create(new OllamaOptions
        {
            BaseUrl = "http://localhost:11434",
            DefaultModel = "llama3.2",
            Timeout = TimeSpan.FromSeconds(30)
        });
        var client = new OllamaClient(httpClient, options);

        // Act
        var result = await client.ChatAsync(null, [
            new ConversationMessage(ConversationRole.System, "system prompt", DateTimeOffset.UtcNow),
            new ConversationMessage(ConversationRole.User, "Hello", DateTimeOffset.UtcNow)
        ], [new ToolDefinition { Name = "example_tool", Description = "Example tool", Parameters = ["--message"] }]);

        // Assert
        Assert.Equal("Hello from Ollama", result.Content);
        Assert.Empty(result.ToolCalls);
        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Equal("http://localhost:11434/api/chat", handler.Request?.RequestUri?.ToString());

        using var document = JsonDocument.Parse(handler.RequestBody!);
        var root = document.RootElement;
        Assert.Equal("llama3.2", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal("system", root.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("system prompt", root.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal("user", root.GetProperty("messages")[1].GetProperty("role").GetString());
        Assert.Equal("Hello", root.GetProperty("messages")[1].GetProperty("content").GetString());
        Assert.Equal("example_tool", root.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
    }

    [Fact]
    public async Task ChatAsync_IncludesOutputDescriptionAndResultExampleInToolDescription()
    {
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { message = new { content = "ok" } })
        });
        var client = new OllamaClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") },
            Options.Create(new OllamaOptions { DefaultModel = "default-model" }));

        await client.ChatAsync(null, [new ConversationMessage(ConversationRole.User, "Hello", DateTimeOffset.UtcNow)], [
            new ToolDefinition
            {
                Name = "search_recipes",
                Description = "Primary recipe discovery tool.",
                OutputDescription = "Recipe discovery cards.",
                ResultExample = new { id = "chicken-teriyaki", name = "Chicken Teriyaki" }
            }
        ]);

        using var document = JsonDocument.Parse(handler.RequestBody!);
        var description = document.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("description").GetString();
        Assert.Contains("Recipe discovery cards", description);
        Assert.Contains("chicken-teriyaki", description);
    }

    [Fact]
    public async Task ChatAsync_UsesProvidedModelWhenPresent()
    {
        // Arrange
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                message = new
                {
                    role = "assistant",
                    content = "Model selected"
                }
            })
        });
        var client = new OllamaClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") },
            Options.Create(new OllamaOptions { DefaultModel = "default-model" }));

        // Act
        await client.ChatAsync("custom-model", [new ConversationMessage(ConversationRole.User, "Hello", DateTimeOffset.UtcNow)], []);

        // Assert
        using var document = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("custom-model", document.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task ChatAsync_WithEmptyUserMessageThrowsArgumentException()
    {
        // Arrange
        var client = new OllamaClient(
            new HttpClient(new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK))),
            Options.Create(new OllamaOptions()));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => client.ChatAsync("llama3.2", [], []));
    }

    [Fact]
    public async Task ChatAsync_ReturnsNativeToolCallsFromOllamaResponse()
    {
        // Arrange
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                message = new
                {
                    role = "assistant",
                    content = "",
                    tool_calls = new[]
                    {
                        new
                        {
                            function = new
                            {
                                name = "example_tool",
                                arguments = new { message = "hello" }
                            }
                        }
                    }
                }
            })
        });
        var client = new OllamaClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") },
            Options.Create(new OllamaOptions { DefaultModel = "default-model" }));

        // Act
        var result = await client.ChatAsync(null, [new ConversationMessage(ConversationRole.User, "Hello", DateTimeOffset.UtcNow)], []);

        // Assert
        Assert.Single(result.ToolCalls);
        Assert.Equal("example_tool", result.ToolCalls[0].Name);
        Assert.Equal("hello", result.ToolCalls[0].Arguments["message"]?.ToString());
    }

    [Fact]
    public async Task ChatAsync_RetriesWithoutToolsWhenOllamaRejectsToolPayload()
    {
        var handler = new CapturingHttpMessageHandler([
            new HttpResponseMessage(HttpStatusCode.BadRequest),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { message = new { content = "fallback ok" } })
            }
        ]);
        var client = new OllamaClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") },
            Options.Create(new OllamaOptions { DefaultModel = "default-model" }));

        var result = await client.ChatAsync(null, [new ConversationMessage(ConversationRole.User, "Hello", DateTimeOffset.UtcNow)], [
            new ToolDefinition { Name = "search_recipes", Description = "Search", Parameters = ["ingredients"] }
        ]);

        Assert.Equal("fallback ok", result.Content);
        Assert.Equal(2, handler.RequestBodies.Count);
        using var fallbackDocument = JsonDocument.Parse(handler.RequestBodies[1]);
        Assert.Empty(fallbackDocument.RootElement.GetProperty("tools").EnumerateArray());
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public CapturingHttpMessageHandler(HttpResponseMessage response)
            : this([response])
        {
        }

        public CapturingHttpMessageHandler(IEnumerable<HttpResponseMessage> responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            if (RequestBody is not null)
            {
                RequestBodies.Add(RequestBody);
            }

            return _responses.Dequeue();
        }
    }
}