import { Injectable } from '@nestjs/common';
import { ISemanticRecipeMetadataProvider, SemanticSearchService } from '../../SemanticSearch/NestJS/semantic-search.service';

export type SourceAttribution = {
  recipeId: string;
  title: string;
  similarityScore: number;
};

export type RetrievalRecipe = {
  recipeId: string;
  title: string;
  description: string;
  tags: string;
  ingredients: string[];
  preparationSteps: string;
  cookingTime: string;
  similarityScore: number;
};

export type RetrievalResult = {
  question: string;
  recipes: RetrievalRecipe[];
  sources: SourceAttribution[];
};

@Injectable()
export class RetrievalService {
  constructor(
    private readonly semanticSearchService: SemanticSearchService,
    private readonly metadataProvider: ISemanticRecipeMetadataProvider,
    private readonly topK = 3,
    private readonly minimumSimilarity = 0.2
  ) {}

  async retrieve(question: string): Promise<RetrievalResult> {
    if (!question?.trim()) {
      return { question, recipes: [], sources: [] };
    }

    const matches = await this.semanticSearchService.search(question, this.topK);
    const recipes: RetrievalRecipe[] = [];
    for (const match of matches.filter((candidate) => candidate.score >= this.minimumSimilarity)) {
      const metadata = await this.metadataProvider.getMetadata(match.recipeId);
      recipes.push({
        recipeId: match.recipeId,
        title: metadata.title,
        description: metadata.description || '',
        tags: metadata.tags || '',
        ingredients: metadata.ingredients || [],
        preparationSteps: metadata.preparationSteps || '',
        cookingTime: metadata.cookingTime || '',
        similarityScore: match.score,
      });
    }

    return {
      question,
      recipes,
      sources: recipes.map((recipe) => ({
        recipeId: recipe.recipeId,
        title: recipe.title,
        similarityScore: recipe.similarityScore,
      })),
    };
  }
}