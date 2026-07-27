# Semantic Search

The .NET semantic search layer generates a query embedding, requests nearest neighbors from the vector store, and enriches ranked matches with repository metadata.

This layer does not call an LLM, build prompts, maintain conversation memory, or implement RAG.

## Completed in .NET

- `SemanticSearchService` orchestrates embedding generation, vector search, and metadata enrichment.
- API endpoint coverage exists through `SemanticSearchController` and integration tests.

## Next Architectural Milestones

TODO: Add structured tracing and diagnostics for semantic search latency and ranking quality.

TODO: Add diagnostics fields in logs for query length, requested topK, returned match count, and match score distribution.

TODO: Add guarded behavior for metadata misses so retrieval quality remains observable without breaking assistant responses.