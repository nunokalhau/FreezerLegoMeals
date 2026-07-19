export type EmbeddingResponse = {
  model: string;
  dimensions: number;
  embedding: number[];
};

export abstract class IEmbeddingService {
  abstract generateEmbedding(text: string): Promise<EmbeddingResponse>;
}