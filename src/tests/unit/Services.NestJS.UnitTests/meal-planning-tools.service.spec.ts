import { resolve } from 'path';
import { ToolExecutor } from '../../../services/Services.NestJS/tool-executor';
import { ToolRegistry } from '../../../services/Services.NestJS/tool-registry';

function createExecutor() {
  const repoRoot = resolve(__dirname, '../../../..');
  const toolsRoot = resolve(repoRoot, 'src/tools');
  return new ToolExecutor(new ToolRegistry(resolve(toolsRoot, 'tool_registry.json')), toolsRoot);
}

describe('Meal planning tools through PythonToolExecutor', () => {
  it('returns recipe discovery cards from search_recipes', async () => {
    const result = await createExecutor().execute('search_recipes', { ingredients: ['chicken'], freezer_friendly: true, limit: 1 });

    expect(result.success).toBe(true);
    expect((result.output as any).total_recipes_found).toBe(1);
    expect((result.output as any).recipes[0]).toMatchObject({
      id: 'salsa_verde_chicken',
      name: 'Salsa Verde Chicken',
      cookTime: 0,
      freezer_friendly: true,
    });
    expect((result.output as any).recipes[0].tags).toContain('high-protein');
    expect((result.output as any).recipes[0].tags).toContain('freezer-friendly');
  });

  it('returns useful outputs for planning tools', async () => {
    const executor = createExecutor();

    const weeklyPlan = await executor.execute('plan_weekly_meals', { number_of_days: 2, meals_per_day: 1 });
    const recipeDetails = await executor.execute('get_recipe', { id: 'salsa_verde_chicken' });
    const replacement = await executor.execute('replace_meal', { current_recipe: 'Turkey Chili', meal_type: 'dinner' });
    const shoppingList = await executor.execute('optimize_shopping_list', { items: [
      { name: 'Fresh Onion', amount: 1, unit: 'unit' },
      { name: 'onion', amount: 2, unit: 'unit' },
    ]});
    const batchPlan = await executor.execute('batch_cooking_plan', { recipes: ['Salsa Verde Chicken'] });
    const converted = await executor.execute('convert_servings', { recipe: 'salsa_verde_chicken', current_servings: 1, target_servings: 2 });
    const substitutions = await executor.execute('ingredient_substitutions', { ingredients: ['chicken', 'unknown'] });

    expect(weeklyPlan.success).toBe(true);
    expect(recipeDetails.success).toBe(true);
    expect(replacement.success).toBe(true);
    expect(shoppingList.success).toBe(true);
    expect(batchPlan.success).toBe(true);
    expect(converted.success).toBe(true);
    expect(substitutions.success).toBe(true);
    expect((weeklyPlan.output as any).days).toHaveLength(2);
    expect((recipeDetails.output as any).numeric_id).toBe(2);
    expect((replacement.output as any).meal_type).toBe('dinner');
    expect((shoppingList.output as any).sections[0].section).toBe('Produce');
    expect((batchPlan.output as any).schedule[0].step).toBe(1);
    expect((converted.output as any).scale_factor).toBe(2);
    expect((substitutions.output as any).suggestions[0].substitutions[0].ingredient).toBe('turkey');
  });
});
