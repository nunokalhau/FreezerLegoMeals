#!/usr/bin/env python3
"""
AI-native entry point for Freezer Lego Meals repository.
This script serves as a single human-friendly CLI wrapper around tools 
in the src/scripts/ folder. AI agents should directly inspect and execute 
the scripts in src/scripts/ rather than relying on this CLI interface.

The ask.py interface understands natural language and routes commands to 
appropriate tools, but the actual implementation logic lives in src/scripts/.
"""

import argparse
import json
import subprocess
import sys
from pathlib import Path
from typing import Dict, Any, List
import re

# Define the available tools based on the scripts in src/scripts/
TOOLS = {
    "plan_meal": {
        "script": "src/scripts/weekly_meal_planner.py",
        "description": "Generate a weekly meal plan with dietary preferences and seasonal constraints",
        "params": ["--dietary-preferences", "--seasonal-constraints", "--num-days"]
    },
    "search_recipes": {
        "script": "src/scripts/search_recipes.py",
        "description": "Search for recipes containing specific ingredients",
        "params": ["--ingredients"]
    },
    "generate_shopping_list": {
        "script": "src/scripts/generate_shopping_list.py",
        "description": "Generate a shopping list based on recipe selections and inventory",
        "params": ["--recipes-file", "--inventory-file"]
    },
    "validate_recipes": {
        "script": "src/scripts/validate_recipe_structure.py",
        "description": "Validate recipe structure and metadata",
        "params": []
    },
    "generate_index": {
        "script": "src/scripts/generate_recipe_index.py",
        "description": "Generate an index of all available recipes",
        "params": []
    }
}

def parse_command(query: str) -> Dict[str, Any]:
    """
    Parse natural language command into structured parameters.
    
    Args:
        query (str): Natural language query
        
    Returns:
        dict: Parsed command with tool and parameters
    """
    query = query.lower().strip()
    
    # Look for keywords to identify the intended tool
    if any(word in query for word in ["plan", "meal", "schedule"]):
        return identify_meal_plan_command(query)
    elif any(word in query for word in ["search", "find", "recipe"]):
        return identify_search_command(query)
    elif any(word in query for word in ["shopping", "list", "buy"]):
        return identify_shopping_list_command(query)
    elif any(word in query for word in ["validate", "check", "structure"]):
        return identify_validation_command(query)
    elif any(word in query for word in ["index", "catalog", "directory"]):
        return identify_index_command(query)
    else:
        # Default to meal planning if nothing specific is found
        return identify_meal_plan_command(query)

def identify_meal_plan_command(query: str) -> Dict[str, Any]:
    """Identify and parse meal planning commands."""
    tool = "plan_meal"
    params = {"num_days": 7}
    
    # Extract dietary preferences
    dietary_prefs = []
    if "vegetarian" in query:
        dietary_prefs.append("vegetarian")
    if "high protein" in query or "high-protein" in query:
        dietary_prefs.append("high-protein")
    if "gluten free" in query:
        dietary_prefs.append("gluten-free")
    if "meatless" in query:
        dietary_prefs.append("meatless")
        
    if dietary_prefs:
        params["dietary_preferences"] = dietary_prefs
    
    # Extract number of days
    day_match = re.search(r'(\d+)\s*(day|week)', query)
    if day_match:
        days = int(day_match.group(1))
        if "week" in query:
            days *= 7
        params["num_days"] = min(days, 30)  # Cap at 30 days
    
    # Extract seasonal constraints
    seasons = ["spring", "summer", "autumn", "fall", "winter"]
    seasonal_constraints = []
    for season in seasons:
        if season in query:
            seasonal_constraints.append(season)
    
    if seasonal_constraints:
        params["seasonal_constraints"] = seasonal_constraints
    
    return {"tool": tool, "params": params}

def identify_search_command(query: str) -> Dict[str, Any]:
    """Identify and parse recipe search commands."""
    tool = "search_recipes"
    ingredients = []
    
    # Simple ingredient extraction (can be enhanced with NLP)
    # Look for common ingredient patterns
    ingredient_words = re.findall(r'(?:with|using|containing)\s+(\w+(?:\s+\w+)*)', query, re.IGNORECASE)
    if not ingredient_words:
        # Extract words that might be ingredients (common food terms)
        food_terms = ["chicken", "beef", "pork", "tofu", "rice", "potato", "carrot", 
                     "broccoli", "spinach", "onion", "garlic", "tomato", "bean", 
                     "pepper", "cucumber", "mushroom", "egg", "salmon"]
        for term in food_terms:
            if term in query.lower():
                ingredients.append(term)
    else:
        # Use matched ingredient words
        for match in ingredient_words:
            ingredients.append(match.strip())
    
    # If no specific ingredients found, try to extract any words that could be ingredients
    if not ingredients:
        words = query.lower().split()
        common_ingredients = ["chicken", "beef", "pork", "tofu", "rice", "potato", 
                             "carrot", "broccoli", "spinach", "onion", "garlic", 
                             "tomato", "bean", "pepper", "cucumber", "mushroom"]
        for word in words:
            if word in common_ingredients:
                ingredients.append(word)
    
    params = {"ingredients": ingredients} if ingredients else {}
    
    return {"tool": tool, "params": params}

def identify_shopping_list_command(query: str) -> Dict[str, Any]:
    """Identify and parse shopping list commands."""
    tool = "generate_shopping_list"
    # Default to standard files
    params = {
        "recipes_file": "src/food/shopping_list_recipes.json",
        "inventory_file": "src/food/shopping_list_inventory.json"
    }
    
    return {"tool": tool, "params": params}

def identify_validation_command(query: str) -> Dict[str, Any]:
    """Identify and parse validation commands."""
    tool = "validate_recipes"
    params = {}
    return {"tool": tool, "params": params}

def identify_index_command(query: str) -> Dict[str, Any]:
    """Identify and parse index generation commands."""
    tool = "generate_index"
    params = {}
    return {"tool": tool, "params": params}

def execute_tool(tool_name: str, params: Dict[str, Any]) -> Dict[str, Any]:
    """
    Execute a tool with the given parameters.
    
    Args:
        tool_name (str): Name of the tool to execute
        params (dict): Parameters for the tool
        
    Returns:
        dict: Execution results
    """
    if tool_name not in TOOLS:
        return {"error": f"Unknown tool: {tool_name}"}
        
    tool = TOOLS[tool_name]
    script_path = tool["script"]
    
    # Check if script exists
    script_file = Path(script_path)
    if not script_file.exists():
        return {"error": f"Tool script not found: {script_path}"}
    
    try:
        # Build command arguments
        cmd = [sys.executable, str(script_file)]
        
        # Add parameters to the command
        for key, value in params.items():
            # Map parameter names to command-line arguments
            if isinstance(value, list):
                for item in value:
                    cmd.extend([f"--{key.replace('_', '-')}", str(item)])
            else:
                cmd.extend([f"--{key.replace('_', '-')}", str(value)])
        
        # Execute the script
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            cwd=str(Path(__file__).parent)
        )
        
        if result.returncode != 0:
            return {"error": f"Tool execution failed: {result.stderr}"}
        
        # Try to parse any JSON output
        output = result.stdout.strip()
        if output:
            try:
                return {"output": json.loads(output)}
            except json.JSONDecodeError:
                return {"output": output}
        
        return {"success": True}
        
    except Exception as e:
        return {"error": f"Execution error: {str(e)}"}

def main(argv=None):
    """Main entry point for the ask.py script."""
    parser = argparse.ArgumentParser(
        description="AI-native CLI wrapper for Freezer Lego Meals",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s "Create a 5-day vegetarian meal plan"
  %(prog)s "Find recipes with chicken and rice"
  %(prog)s "Generate shopping list"
        
Note: AI agents should inspect tools in src/scripts/ directory and execute them directly
rather than using this CLI wrapper. This script is for human convenience only.
        """
    )
    
    parser.add_argument(
        "query",
        help="Natural language query about meal planning or recipes"
    )
    
    args = parser.parse_args(argv)
    
    # Parse the command
    parsed_command = parse_command(args.query)
    
    # Execute the tool
    result = execute_tool(parsed_command["tool"], parsed_command["params"])
    
    # Output result as JSON
    print(json.dumps(result, indent=2))
    

if __name__ == "__main__":
    main()