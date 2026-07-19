import { Injectable } from '@nestjs/common';
import { IEmbeddingService } from '../../Embedding.NestJS/embedding.service.interface';
import { IVectorStore } from '../../VectorStores/NestJS/local-vector-store';

export type SemanticSearchResult = {
  recipeId: string;
  title: string;
  score: number;
  matchedText: string;
  reason: string;
};

export type RecipeMetadata = {
  recipeId: string;
  title: string;
  matchedText: string;
};

export abstract class ISemanticRecipeMetadataProvider {
  abstract getMetadata(recipeId: string): Promise<RecipeMetadata>;
}

@Injectable()
export class SemanticSearchService {
  constructor(
    private readonly embeddingService: IEmbeddingService,
    private readonly vectorStore: IVectorStore,
    private readonly metadataProvider: ISemanticRecipeMetadataProvider
  ) {}

  async search(query: string, topK = 5): Promise<SemanticSearchResult[]> {
    if (!query?.trim() || topK <= 0) {
      return [];
    }

    const queryEmbedding = await this.embeddingService.generateEmbedding(query);
    const matches = await this.vectorStore.search(queryEmbedding.embedding, topK);
    const results: SemanticSearchResult[] = [];
    for (const match of matches) {
      const metadata = await this.metadataProvider.getMetadata(match.recipeId);
      results.push({
        recipeId: match.recipeId,
        title: metadata.title,
        score: Number(match.score.toFixed(6)),
        matchedText: metadata.matchedText,
        reason: `High semantic similarity between the query and ${metadata.title}.`,
      });
    }

    return results;
  }
}