# Freezer Lego Meals - Script Capabilities

This document provides a comprehensive overview of all available automation scripts in the Freezer Lego Meals project. Each script is designed to help users plan, organize, and optimize their meal-preparation workflow without requiring deep technical knowledge.

## Overview of Scripts

### 1. Weekly Meal Planner
**Purpose:** Generates structured meal plans for multiple days based on available recipes in the repository.

**Available parameters:**
- `--food-dir`: Path to the food directory (defaults to project's food directory)
- `--start-date`: Start date for the plan (YYYY-MM-DD format), defaults to today
- `--num-days`: Number of days to plan for (default 7)
- `--output`: Optional output path for JSON plan file
- `--markdown-output`: Optional output path for markdown summary
- `--dietary-preferences`: List of dietary constraints like "vegetarian" or "high-protein"
- `--seasonal-constraints`: List of seasonal factors to consider

**Use case:** Planning a week's worth of meals while avoiding repetition and considering dietary preferences.

### 2. Recipe Search Tool
**Purpose:** Finds recipes that contain specific ingredients.

**Available parameters:**
- `--ingredients` or `-i`: List of ingredient names to search for (OR logic)
- `--recipe-id`: Specific recipe ID to retrieve detailed information

**Use case:** Finding recipes that work with ingredients you already have on hand.

### 3. Shopping List Generator
**Purpose:** Creates optimized shopping lists by subtracting inventory items from recipe requirements.

**Available parameters:**
- `--food-dir`: Path to the food directory (defaults to project's food directory)
- `--recipes-file`: JSON file containing recipe paths/IDs to include
- `--inventory-file`: JSON file containing existing inventory items
- `--output`: Optional output path for JSON shopping list
- `--markdown-output`: Optional output path for markdown shopping list

**Use case:** Generating shopping lists that exclude items you already have.

### 4. Recipe Index Generator
**Purpose:** Creates a comprehensive index of all recipes in the repository.

**Usage:** Run from project root: `python src/scripts/generate_recipe_index.py`

**Output:** Generates `src/food/recipe_index.md` with categorized recipe list.

**Use case:** Getting an overview of all available recipes in the system.

### 5. Recipe Structure Validator
**Purpose:** Checks recipe files for structural consistency and adherence to templates.

**Usage:** Run from project root: `python src/scripts/validate_recipe_structure.py`

**Output:** Generates `src/food/recipe_validation.json` and `src/food/recipe_validation.md` with detailed validation reports.

**Use case:** Ensuring recipes follow the required format for consistency.

## Intent Recognition Patterns

### Recipe Planning Requests
- "Plan my week for next Monday through Friday"
- "Generate a meal plan considering vegetarian preferences"
- "I need a 7-day plan that avoids repetition"

### Ingredient-Based Search
- "What can I cook with chicken and rice?"
- "Show me anything that uses tofu"
- "Find recipes with onions and garlic"

### Shopping List Requests
- "Make me a shopping list for these recipes"
- "What do I need to buy considering my inventory?"
- "List ingredients for my weekly meals"

## Sisyphus Role Automation

The Sisyphus role would recognize user queries by analyzing their intent and automatically suggest the appropriate script:

1. **Planning requests**: Automatically route to Weekly Meal Planner
2. **Ingredient searches**: Route to Recipe Search Tool
3. **Shopping list generation**: Route to Shopping List Generator
4. **General recipe information**: Route to Recipe Index Generator or Structure Validator

Each recognition pattern would be matched against the user's query using keyword analysis and intent classification to determine the best script for execution.