# Embedding Indexing Pipeline

This folder contains the canonical recipe embedding indexing pipeline. It converts recipes into semantic documents, calls the Python embedding client, and writes local embedding JSON files under `data/embeddings/`.

The pipeline does not implement vector search, retrieval, RAG, ChromaDB, FAISS, pgvector, Redis, or semantic search. Semantic retrieval reads the files produced here from the vector store layer.

Run from the repository root:

```powershell
python src\scripts\embeddings\generate_embeddings.py
```

Optional configuration:

```powershell
python src\scripts\embeddings\generate_embeddings.py --model nomic-embed-text --ollama-url http://localhost:11434 --output-dir data\embeddings
```

TODO: Add vector database configuration only if a future phase outgrows local disk-backed vector stores.

TODO: Consider Redis caching in the indexing pipeline if repeated generation becomes expensive.