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

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public CapturingHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return _response;
        }
    }
}