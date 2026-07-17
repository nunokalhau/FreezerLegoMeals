# Scripts

This folder contains automation and tooling scripts organized by functionality.

## Structure

- `recipes/` - Recipe search, index generation, and structure validation
- `shopping/` - Shopping list generation and optimization  
- `meal_planning/` - Weekly meal planning and batch preparation scheduling
- `nutrition/` - Nutritional analytics and analysis
- `substitutions/` - Ingredient substitution recommendations

## Purpose

Each subdirectory contains scripts that help maintain, validate, or generate parts of the repository.

## Recipes Scripts
- `search_recipes.py` - Search for recipes containing specific ingredients
- `get_recipe_ingredients.py` - Get ingredients for a specified recipe
- `generate_recipe_index.py` - Generate an index of all available recipes  
- `validate_recipe_structure.py` - Validate recipe structure and metadata

## Shopping Scripts
- `generate_shopping_list.py` - Generate shopping lists from selected recipes and optional inventory data
- `shopping_list_optimizer.py` - Optimize shopping lists with inventory data  

## Meal Planning Scripts
- `weekly_meal_planner.py` - Generate weekly meal plans with dietary preferences  
- `batch_preparation_scheduler.py` - Plan batch cooking sessions

## Nutrition Scripts  
- `nutrition_analytics.py` - Analyze nutritional information

## Substitution Scripts
- `ingredient_substitution_finder.py` - Find ingredient substitutions

## Usage Examples

```bash
# Recipe search
python src/scripts/recipes/search_recipes.py --ingredients "chicken" "rice"

# Shopping list generation  
python src/scripts/shopping/generate_shopping_list.py --recipes-file path/to/recipe-list.json

# Weekly meal planning
python src/scripts/meal_planning/weekly_meal_planner.py --dietary-preferences "vegetarian"
```