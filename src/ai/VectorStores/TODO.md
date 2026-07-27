# Vector Stores

The .NET reference implementation now uses ChromaDB v2 as the primary vector store via HTTP APIs.

## Completed in .NET

- ChromaDB collection create/get-or-create is implemented.
- Query execution against collection id endpoints is implemented.
- Similarity ranking maintains cosine-based scoring behavior when embeddings are returned.
- Integration coverage exists for collection lifecycle and query behavior.

## Next Architectural Milestones

TODO: Add structured tracing and latency metrics for all vector store operations (collection resolution, query, and ranking).

TODO: Add dependency-level resilience strategy (timeouts, retry policy, and bounded failure behavior) for ChromaDB outages and partial responses.

TODO: Add vector retrieval diagnostics payloads in logs (requested topK, returned ids, returned distances/embeddings availability, and final score ordering).

TODO: Define a clean vector store capability contract for future adapters (e.g., pgvector/Qdrant) without changing orchestration or semantic search layers.