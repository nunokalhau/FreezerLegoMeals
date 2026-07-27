# AI Evaluation Framework (.NET)

## Purpose

This framework provides deterministic, automated evaluation of assistant behavior in the .NET reference implementation.

It validates:

- routing decisions
- retrieval relevance
- semantic search quality
- tool selection
- tool execution
- memory retrieval
- grounded responses
- overall answer quality

## Architecture

The production framework lives under:

- src/ai/Evaluation/DotNet

It is integrated into application DI in:

- src/api/WebApi.DotNet/Program.cs

Tests consume the same production abstractions and services.

Core components:

- IAiEvaluationService / AiEvaluationService: Executes scenarios programmatically.
- AiEvaluationScenario: Declares deterministic inputs and expected quality gates.
- IAiEvaluationDimension implementations: Modular validators for each behavior axis.
- Trace decorators (assistant/orchestrator/ollama/tools/retrieval): Capture execution data and optionally serve scripted LLM/tool outputs for deterministic runs.

## Deterministic Scenario Suite

Current scenarios:

- tool-routing-and-execution
- grounded-rag-retrieval-quality
- memory-retrieval-and-direct-answer

Each scenario uses scripted Ollama outputs and deterministic retrieval/semantic fixtures. No network calls are required.

## Running Evaluations

Run deterministic evaluation through tests:

```powershell
dotnet test src/tests/unit/Services.DotNet.UnitTests/Services.DotNet.UnitTests.csproj -v minimal
```

Run end-to-end evaluation integration tests:

```powershell
dotnet test src/tests/integration/WebApi.DotNet.IntegrationTests/WebApi.DotNet.IntegrationTests.csproj --logger "console;verbosity=minimal"
```

Run evaluations programmatically from any DI scope:

```csharp
var service = scope.ServiceProvider.GetRequiredService<IAiEvaluationService>();
var reports = await service.EvaluateDefaultScenariosAsync();
```

The evaluation suite is part of normal unit test execution, so CI can run it automatically.

## Extensibility

To add a new evaluation dimension:

1. Implement IAiEvaluationDimension.
2. Register the dimension in AddAiEvaluationFramework.
3. Add expectations to one or more scenarios.

To add a new scenario:

1. Add a scenario factory in DefaultAiEvaluationScenarioCatalog.
2. Provide scripted responses/tool results when deterministic control is needed.
3. Define explicit expectations for the dimensions it should validate.

This structure is intentionally portable so the same architecture can be mirrored in NestJS and Python later.
