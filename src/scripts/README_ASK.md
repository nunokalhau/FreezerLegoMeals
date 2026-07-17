# AI-native Entry Point

The `ask.py` script serves as the single public entry point for AI agents interacting with the Freezer Lego Meals repository. It accepts natural language queries and routes them to appropriate tools in the system.

## Features

- **Natural Language Processing**: Parses commands like "Create a 5-day vegetarian meal plan"
- **Tool Routing**: Routes queries to appropriate scripts based on intent recognition
- **Parameter Extraction**: Extracts dietary preferences, constraints, and other parameters from queries
- **Script Execution**: Executes existing Python scripts with proper arguments
- **Structured Output**: Returns clean, machine-readable JSON results

## Supported Commands

1. **Meal Planning**: "Create a 5-day vegetarian meal plan", "Plan 7-day schedule"
2. **Recipe Search**: "Find recipes with chicken and rice", "Search for tofu recipes"
3. **Shopping Lists**: "Generate shopping list", "Create grocery list"
4. **Validation**: "Check recipe structure", "Validate recipes"
5. **Index Generation**: "Generate recipe index", "List all recipes"

## Usage

```bash
python ask.py "Create a 5-day vegetarian meal plan"
```

The script returns structured JSON output that AI agents can easily consume and process.