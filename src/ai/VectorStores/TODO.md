# Vector Stores

Phase 2 uses local disk-backed vector stores that read recipe embedding JSON files from `data/embeddings/`, cache them in process memory, and rank matches with cosine similarity.

Do not add ChromaDB, FAISS, pgvector, Qdrant, or Redis for this phase.