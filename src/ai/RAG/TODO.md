# Retrieval-Augmented Generation

RAG is implemented here as reusable retrieval and prompt-building components for Python, .NET, and NestJS.

## Responsibilities

- `RetrievalService` receives a user question, calls `SemanticSearchService`, retrieves the top recipe matches, and returns structured context.
- `PromptBuilder` receives the user question plus retrieved recipes and renders the final repository-grounded prompt from `prompts/rag_prompt.txt`.
- `AssistantOrchestrator` owns Assistant workflow coordination. It keeps existing tool calling first, then uses RAG for repository-knowledge questions, then falls back to direct Ollama answers for general chat.

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

TODO: Add Conversation Memory in `src/ai/Memory` when that phase starts.

TODO: Add Redis only when distributed caching or memory is introduced.

TODO: Add ChromaDB or another vector database only if local disk-backed vectors become insufficient.

TODO: Add autonomous AI-agent research only after its boundaries are explicitly defined outside deterministic Assistant orchestration.