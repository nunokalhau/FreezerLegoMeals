# Available Tools

This document describes all available tools in the Freezer Lego Meals repository. These are the capabilities that can be invoked through natural language commands.

## Tool Listing

### weekly_meal_planner
**Purpose**: Generate balanced weekly meal plans with dietary preferences and seasonal constraints  
**When to use**: When someone needs a structured meal plan for multiple days
**Examples**:
- "Create a 5-day vegetarian meal plan"
- "Plan weekend meals for 7 days with high-protein options"

### ingredient_search  
**Purpose**: Find recipes containing specific ingredients or look up recipes by ID
**When to use**: When someone wants to discover recipes based on available ingredients
**Examples**:
- "Find recipes with chicken" 
- "Look up recipe by ID 123"

### get_recipe_ingredients
**Purpose**: Get detailed ingredient information for a specific recipe
**When to use**: When someone needs exact ingredient quantities and units for a recipe
**Examples**:
- "Show ingredients in Black Beans"
- "What are the ingredients for Salsa Verde Chicken?"

### generate_shopping_list
**Purpose**: Create shopping lists by parsing recipes and subtracting inventory
**When to use**: When someone needs grocery shopping items with existing inventory accounted for
**Examples**:
- "Generate shopping list for 3 recipes"  
- "Create grocery list with inventory subtraction"

### generate_recipe_index
**Purpose**: Build categorized index of all recipes 
**When to use**: When someone wants to see organized listing of all available recipes
**Examples**:
- "Generate recipe index by category"
- "List all recipes with their source paths"

### validate_recipe_structure
**Purpose**: Verify recipes follow standard markdown structure  
**When to use**: When ensuring recipe consistency or quality control
**Examples**:
- "Validate all recipes follow standard structure"
- "Check recipe markdown files for consistency"

### nutrition_analytics
**Purpose**: Analyze nutritional information and dietary patterns
**When to use**: When performing dietary analysis or planning balanced meals  
**Examples**:
- "Analyze nutrition of meal plan"
- "Calculate nutritional values for selected recipes"

### batch_preparation_scheduler
**Purpose**: Schedule optimal batch cooking based on freezer capacity
**When to use**: For large-scale meal preparation planning
**Examples**:
- "Schedule batch cooking for 10 recipes"  
- "Plan preparation time with freezer constraints"

### ingredient_substitution_finder
**Purpose**: Suggest ingredient alternatives when key ingredients aren't available
**When to use**: When someone needs recipe flexibility or ingredient substitutions
**Examples**:
- "Find substitute for chicken"
- "What can I use instead of tofu?"

### shopping_list_optimizer
**Purpose**: Group shopping items by store aisles for efficiency  
**When to use**: When organizing grocery trips by product categories
**Examples**:
- "Optimize shopping list by store aisle"
- "Organize ingredients by grocery store section"

## Common Mistakes

1. **Requesting unavailable tools**: AI agents should not invent tool names that don't exist
2. **Misunderstanding input parameters**: Tool parameters are specific to each tool  
3. **Assuming output format**: Always use tools to get actual results instead of guessing
4. **Overcomplicating simple requests**: Use the most appropriate simple tool rather than complex combinations

## Anti-patterns

1. **Hallucination**: Answering questions without executing existing tools
2. **Memory over execution**: Reasoning from stored knowledge instead of running scripts 
3. **Too many steps**: Using multiple tools when one would suffice
4. **Inconsistent parameter usage**: Using parameters in unexpected formats

## Tips for Maximum Effectiveness

1. **Be specific with tools**: Use exact tool names from this list  
2. **Match intent to tool**: Look at what action you want to perform
3. **Verify outputs**: Always use tools to get actual data, never assume
4. **Keep it simple**: Use fewer complex requests rather than trying to do too much at once

## Output Format Conventions  

All tools output structured JSON for programmatic consumption plus markdown summaries for human readability. This dual format ensures consistent information exchange regardless of how tools are executed.