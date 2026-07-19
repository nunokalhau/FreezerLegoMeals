import { Injectable } from '@nestjs/common';
import { RecipeRepository } from '../../repositories/Repository.NestJS/recipe.repository';
import { ISemanticRecipeMetadataProvider, RecipeMetadata } from '../../ai/SemanticSearch/NestJS/semantic-search.service';

@Injectable()
export class RepositorySemanticMetadataProvider implements ISemanticRecipeMetadataProvider {
  private cache: Map<string, RecipeMetadata> | undefined;

  constructor(private readonly recipeRepository: RecipeRepository) {}

  async getMetadata(recipeId: string): Promise<RecipeMetadata> {
    if (!this.cache) {
      const recipes = await this.recipeRepository.getRecipes();
      this.cache = new Map(recipes.map((recipe) => [
        String(recipe.id),
        {
          recipeId: String(recipe.id),
          title: recipe.name,
          matchedText: [
            recipe.name,
            recipe.notes,
            recipe.tags,
            recipe.prepping,
            (recipe.recipeIngredients || []).map((ingredient) => ingredient.ingredient?.name).filter(Boolean).join(', '),
          ].filter(Boolean).join(' | '),
        },
      ]));
    }

    return this.cache.get(recipeId) || { recipeId, title: `Recipe ${recipeId}`, matchedText: '' };
  }
}