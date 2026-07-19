# Semantic Search

Phase 2 semantic retrieval lives here. Services in this folder generate a query embedding, ask a vector store for nearest recipe embeddings, and enrich ranked matches with recipe metadata.

This layer does not call an LLM, build prompts, maintain conversation memory, or implement RAG.