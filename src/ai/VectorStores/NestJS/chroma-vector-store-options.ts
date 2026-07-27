export type ChromaVectorStoreOptions = {
  baseUrl: string;
  tenant: string;
  database: string;
  collectionName: string;
  timeoutMs: number;
};

export const CHROMA_VECTOR_STORE_OPTIONS = Symbol('CHROMA_VECTOR_STORE_OPTIONS');

export function createChromaVectorStoreOptions(): ChromaVectorStoreOptions {
  return {
    baseUrl: (process.env.CHROMA_VECTOR_STORE_BASE_URL || 'http://localhost:8001').replace(/\/+$/, ''),
    tenant: process.env.CHROMA_VECTOR_STORE_TENANT || 'default_tenant',
    database: process.env.CHROMA_VECTOR_STORE_DATABASE || 'default_database',
    collectionName: process.env.CHROMA_VECTOR_STORE_COLLECTION_NAME || 'recipe_embeddings',
    timeoutMs: Number(process.env.CHROMA_VECTOR_STORE_TIMEOUT_MS || 30000),
  };
}