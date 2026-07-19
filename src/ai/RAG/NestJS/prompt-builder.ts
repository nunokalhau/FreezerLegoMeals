import { readFileSync } from 'fs';
import { RetrievalRecipe } from './retrieval.service';

export class PromptBuilder {
  constructor(private readonly template: string) {
    if (!template?.trim()) {
      throw new Error('Prompt template is required');
    }
  }

  static fromFile(templatePath: string): PromptBuilder {
    return new PromptBuilder(readFileSync(templatePath, 'utf8'));
  }

  build(question: string, recipes: RetrievalRecipe[]): string {
    return this.template
      .replace('{recipes}', this.formatRecipes(recipes))
      .replace('{question}', question.trim());
  }

  private formatRecipes(recipes: RetrievalRecipe[]): string {
    if (recipes.length === 0) {
      return 'No relevant recipes were retrieved.';
    }

    return recipes.map((recipe) => this.formatRecipe(recipe)).join('\n\n');
  }

  private formatRecipe(recipe: RetrievalRecipe): string {
    return [
      `Recipe ID: ${recipe.recipeId}`,
      `Title: ${recipe.title}`,
      `Description: ${recipe.description || 'Not specified'}`,
      `Tags: ${recipe.tags || 'Not specified'}`,
      `Ingredients: ${recipe.ingredients.length > 0 ? recipe.ingredients.join(', ') : 'Not specified'}`,
      `Preparation steps: ${recipe.preparationSteps || 'Not specified'}`,
      `Cooking time: ${recipe.cookingTime || 'Not specified'}`,
      `Similarity score: ${recipe.similarityScore.toFixed(6)}`,
    ].join('\n');
  }
}