using System.Net;
using System.Text;
using Embedding.DotNet;
using Microsoft.Extensions.Options;
using Xunit;

namespace Services.DotNet.UnitTests;

public class OllamaEmbeddingServiceTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_PostsToOllamaAndReturnsVector()
    {
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"embedding\":[0.1,0.2,0.3]}", Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var service = new OllamaEmbeddingService(httpClient, Options.Create(new EmbeddingOptions { Model = "test-model" }));

        var response = await service.GenerateEmbeddingAsync("Chicken curry");

        Assert.Equal("test-model", response.Model);
        Assert.Equal(3, response.Dimensions);
        Assert.Equal(3, response.Embedding.Count);
        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Equal("http://localhost:11434/api/embeddings", handler.Request?.RequestUri?.ToString());
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithBlankTextThrowsArgumentException()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        var service = new OllamaEmbeddingService(httpClient, Options.Create(new EmbeddingOptions()));

        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateEmbeddingAsync(" "));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StubHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(_response);
        }
    }
}