# Evaluation.DotNet

Production AI evaluation framework for the .NET assistant architecture.

## Goals

- Evaluate assistant behavior across routing, retrieval, semantic search, tools, memory, grounded answers, and response quality.
- Run evaluations programmatically through DI via IAiEvaluationService.
- Support deterministic scenarios using scripted LLM/tool outputs.
- Support real end-to-end evaluations using the live runtime dependencies.

## Key Components

- IAiEvaluationService / AiEvaluationService
- IAiEvaluationScenarioCatalog / DefaultAiEvaluationScenarioCatalog
- IAiEvaluationDimension implementations
- IAiEvaluationTraceContext and runtime decorators:
  - EvaluationOllamaClient
  - EvaluationToolExecutor
  - EvaluationRetrievalService
  - EvaluationAssistantOrchestrator

## Registration

Use AddAiEvaluationFramework from this project and wrap runtime registrations as done in WebApi.DotNet Program.

## Testing

- Unit tests consume this production framework directly.
- Integration tests run deterministic default scenarios and optional real-Ollama evaluation smoke tests.
