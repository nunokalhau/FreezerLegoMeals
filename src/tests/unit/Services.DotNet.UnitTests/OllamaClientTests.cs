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
        var result = await client.ChatAsync(null, "Hello");

        // Assert
        Assert.Equal("Hello from Ollama", result);
        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Equal("http://localhost:11434/api/chat", handler.Request?.RequestUri?.ToString());

        using var document = JsonDocument.Parse(handler.RequestBody!);
        var root = document.RootElement;
        Assert.Equal("llama3.2", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal("user", root.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("Hello", root.GetProperty("messages")[0].GetProperty("content").GetString());
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
        await client.ChatAsync("custom-model", "Hello");

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
        await Assert.ThrowsAsync<ArgumentException>(() => client.ChatAsync("llama3.2", " "));
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