using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestration.DotNet;
using Xunit;

namespace Services.DotNet.UnitTests;

public class AssistantOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_SelectsFirstAgentThatCanHandleContext()
    {
        var context = CreateContext();
        var skippedAgent = new StubAgent("SkippedAgent", false, "skipped");
        var selectedAgent = new StubAgent("SelectedAgent", true, "selected response");
        var orchestrator = new AssistantOrchestrator([skippedAgent, selectedAgent], NullLogger<AssistantOrchestrator>.Instance);

        var result = await orchestrator.ExecuteAsync(context);

        Assert.Equal("selected response", result.FinalResponse);
        Assert.Equal("SelectedAgent", result.SelectedAgent);
        Assert.False(skippedAgent.WasExecuted);
        Assert.True(selectedAgent.WasExecuted);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoAgentCanHandle_ReturnsObservableError()
    {
        var orchestrator = new AssistantOrchestrator([new StubAgent("InactiveAgent", false, "unused")], NullLogger<AssistantOrchestrator>.Instance);

        var result = await orchestrator.ExecuteAsync(CreateContext());

        Assert.Equal("none", result.SelectedAgent);
        Assert.Contains("No assistant agent", result.FinalResponse);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("NoAgent", result.ExecutionSteps);
    }

    private static OrchestratorContext CreateContext() => new(
        "Hello",
        DateTimeOffset.UtcNow,
        "correlation-1",
        new Dictionary<string, object?>(),
        "conversation-1",
        [],
        [],
        Options.Create(new AssistantOptions()).Value);

    private sealed class StubAgent : IAgent
    {
        private readonly bool _canHandle;
        private readonly string _response;

        public StubAgent(string name, bool canHandle, string response)
        {
            Name = name;
            _canHandle = canHandle;
            _response = response;
        }

        public string Name { get; }

        public bool WasExecuted { get; private set; }

        public bool CanHandle(OrchestratorContext context) => _canHandle;

        public Task<OrchestratorResult> ExecuteAsync(OrchestratorContext context, CancellationToken cancellationToken = default)
        {
            WasExecuted = true;
            return Task.FromResult(new OrchestratorResult(_response, Name, [], [], ["Assistant", "AssistantOrchestrator", Name], TimeSpan.FromMilliseconds(1), [], context.MessagesToPersist));
        }
    }
}