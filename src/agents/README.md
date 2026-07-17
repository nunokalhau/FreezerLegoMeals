# Meal Agent

The Meal Agent is designed to answer questions about recipes and meals using the project's own data.

## Features

- **Recipe Search**: Find meals containing specific ingredients  
- **Recipe Details**: Retrieve detailed information about specific recipes
- **Natural Language Processing**: Understand queries about meals and ingredients

## Usage Examples

```python
from meal_agent import MealAgent

agent = MealAgent()

# Find meals with specific ingredients
result = agent.find_meals_with_ingredients("Find meals with chicken")
print(result)

# Get details for a specific recipe
recipe_details = agent.get_recipe_details(42)
print(recipe_details)
```

## Integration

The agent connects to the existing SQLite database (`data/recipes_local.db`) and uses the same patterns as the project's existing scripts like `search_recipes.py`.

## Data Sources

The agent leverages:
- Recipe database at `data/recipes_local.db`
- Existing database schema from `src/scripts/`
- Natural language parsing of user queries


# Shopping Agent

The Shopping Agent generates shopping lists from one or more recipes with advanced features.

## Features

- **Merge duplicate ingredients**: Automatically combine same ingredients from different recipes
- **Calculate total quantities**: Aggregate amounts for accurate shopping lists  
- **Group ingredients by category**: Organize items into logical groups (proteins, vegetables, grains, etc.)
- **Scale recipes**: Adjust ingredient quantities based on serving size
- **Clean output**: Generate well-formatted shopping list data

## Usage Examples

```python
from shopping_agent import ShoppingAgent

agent = ShoppingAgent()

# Generate shopping list for single recipe
result = agent.generate_shopping_list(["Chicken Curry"])
print(result)

# Generate shopping list for multiple recipes with scaling
result = agent.generate_shopping_list(["Chicken Curry", "Beef Stir Fry"], scale_factor=2.0)
print(result)

# Get categorized shopping list
result = agent.generate_shopping_list(["Chicken Curry"], group_by_category=True)
print(result)
```

## Integration

The agent connects to the existing SQLite database (`data/recipes_local.db`) and uses the same patterns as the project's existing scripts.

## Data Sources

The agent leverages:
- Recipe database at `data/recipes_local.db`
- Existing database schema from `src/scripts/`
- Ingredients structured in recipe_ingredients table