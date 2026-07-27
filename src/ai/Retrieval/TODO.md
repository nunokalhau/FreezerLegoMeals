# Retrieval

Retrieval orchestration is implemented in the .NET reference architecture and should remain separate from embedding generation concerns.

## Completed in .NET

- Retrieval logic is implemented in `src/ai/RAG/DotNet/RetrievalService.cs`.
- Query embedding generation remains in the embedding service.
- Semantic ranking remains in semantic search and vector store layers.
- Retrieval applies a minimum similarity threshold before returning context.

## Next Architectural Milestones

TODO: Keep retrieval as a dedicated orchestration boundary and avoid embedding-client coupling.

TODO: Add retrieval diagnostics with explicit decision reasons:
- empty/invalid query
- no semantic matches returned
- matches below similarity threshold
- context accepted

TODO: Add retrieval latency and relevance distribution logs for debugging and offline evaluation.

TODO: Add optional retrieval policy strategies (threshold profile, adaptive topK) selected by orchestration policy, not by embedding/vector store implementations.