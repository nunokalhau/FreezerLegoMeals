# Retrieval-Augmented Generation

RAG in this repository is now implemented and productionized first in .NET. Python and NestJS should follow the same architecture over time.

## Responsibilities

- `RetrievalService` receives a user question, calls `SemanticSearchService`, retrieves the top recipe matches, and returns structured context.
- `PromptBuilder` receives the user question plus retrieved recipes and renders the final repository-grounded prompt from `prompts/rag_prompt.txt`.
- `AssistantOrchestrator` owns Assistant workflow coordination. It keeps existing tool calling first, then uses RAG for repository-knowledge questions, then falls back to direct Ollama answers for general chat.

## Completed in .NET

- RAG retrieval flow is implemented in `src/ai/RAG/DotNet/RetrievalService.cs`.
- Prompt rendering is implemented in `src/ai/RAG/DotNet/PromptBuilder.cs`.
- Assistant orchestration uses RAG for repository-knowledge prompts in `src/orchestration/DotNet/MealPlanningAgent.cs`.
- Source attribution is appended to assistant responses for retrieval-backed answers.
- Redis-backed conversation persistence and fallback memory are already implemented in `src/ai/Memory/DotNet` and wired in `src/api/WebApi.DotNet/Program.cs`.
- ChromaDB vector retrieval is implemented in `src/ai/VectorStores/DotNet/ChromaVectorStore.cs` and wired in `src/api/WebApi.DotNet/Program.cs`.
- Ollama model capability detection is implemented with provider abstractions in `src/services/Services.DotNet` and wired in `src/api/WebApi.DotNet/Program.cs` so tool payloads are omitted automatically after runtime discovery.

## Hallucination Prevention

RAG prompts instruct the model to answer only from provided repository context and to clearly say when the repository does not contain enough information.

If retrieval returns no relevant recipes, AssistantService returns a no-repository-information response instead of asking the model to invent an answer.

## Source Attribution

Assistant responses keep the existing public response shape and append source attribution in the response text:

```text
Sources:
- 1: Spicy Chicken (similarityScore: 0.910000)
```

This preserves the Assistant API contract while making recipe usage visible for debugging.

## How RAG Differs

- Embeddings generate vectors for text.
- Semantic Search ranks existing recipe embeddings by meaning.
- Tool Calling executes deterministic commands such as shopping lists, meal planning, conversions, and substitutions.
- RAG uses Semantic Search results as repository context for an LLM answer.

## Future TODOs

TODO: Add policy-based assistant routing so tool-first, retrieval-first, and direct-LLM paths are selected deterministically from explicit orchestration rules instead of keyword heuristics alone.

TODO: Add retrieval quality gates (decision reasons, no-context diagnostics, score distribution logging, and retrieval latency tracking) as first-class observability artifacts.

TODO: Add resilience policies around LLM, vector store, and tool execution boundaries (retry/backoff/circuit-breaker strategy per dependency).

TODO: Add a persistent model capability cache provider (Redis or database) behind the .NET `IModelCapabilitiesCache` abstraction so discovered capabilities survive application restarts.

TODO: Add memory summarization and token-budget-aware context assembly so long-running conversations remain grounded and efficient.

TODO: Add autonomous AI-agent research only after boundaries and safety guardrails are explicitly defined outside deterministic Assistant orchestration.