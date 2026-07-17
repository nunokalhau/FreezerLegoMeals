from __future__ import annotations

import json
import sqlite3
import random
from datetime import datetime, timedelta
from pathlib import Path
from typing import List, Dict, Any
from collections import defaultdict

def generate_weekly_plan(
    food_dir: Path,
    start_date: str | None = None,
    num_days: int = 7,
    output_path: Path | None = None,
    markdown_path: Path | None = None,
    dietary_preferences: List[str] | None = None,
    seasonal_constraints: List[str] | None = None,
) -> tuple[Path, Path | None]:
    """Generate a weekly meal plan based on available recipes and ingredients.

    Args:
        food_dir: Path to the food directory
        start_date: Start date for the plan (YYYY-MM-DD format)
        num_days: Number of days to plan for
        output_path: Optional output path for JSON plan
        markdown_path: Optional output path for markdown plan
        dietary_preferences: List of dietary preferences (vegetarian, high-protein, etc.)
        seasonal_constraints: List of seasonal constraints

    Returns:
        Tuple of (JSON output path, markdown output path)
    """
    food_dir = food_dir.resolve()
    if output_path is None:
        output_path = food_dir / "weekly_plan.json"
    else:
        output_path = output_path.resolve()

    if markdown_path is None:
        markdown_path = food_dir / "weekly_plan.md"
    else:
        markdown_path = markdown_path.resolve()
    
    # Get database path from data directory
    db_path = Path(__file__).resolve().parents[3] / "data" / "recipes_local.db"
    
    # Connect to SQLite database
    conn = sqlite3.connect(db_path)
    
    try:
        # If no start date specified, use today
        if start_date is None:
            start_date_obj = datetime.now().date()
        else:
            start_date_obj = datetime.strptime(start_date, "%Y-%m-%d").date()
        
        plan = {
            "generated_by": "weekly_meal_planner.py",
            "start_date": start_date_obj.isoformat(),
            "num_days": num_days,
            "dietary_preferences": dietary_preferences or [],
            "seasonal_constraints": seasonal_constraints or [],
            "meals": [],
        }

        # Fetch all recipes with their information
        cursor = conn.execute("""
            SELECT r.id, r.name, r.tags, r.servings, r.combinations, 
                   GROUP_CONCAT(ri.ingredient_id) as ingredient_ids
            FROM recipes r
            LEFT JOIN recipe_ingredients ri ON r.id = ri.recipe_id
            GROUP BY r.id
            ORDER BY r.name
        """)
        
        recipes = []
        for row in cursor.fetchall():
            recipe_id, name, tags, servings, combinations, ingredient_ids = row
            # Split ingredient IDs if they exist
            if ingredient_ids:
                ingredient_list = [int(x) for x in ingredient_ids.split(',')]
            else:
                ingredient_list = []
                
            recipes.append({
                "id": recipe_id,
                "name": name,
                "tags": tags.split(",") if tags else [],
                "servings": servings or 1,
                "combinations": combinations.split(",") if combinations else [],
                "ingredient_ids": ingredient_list
            })
        
        cursor = conn.execute("SELECT id, name, tags FROM recipes")
        recipe_tags_map = {}
        for row in cursor.fetchall():
            recipe_id, name, tags = row
            recipe_tags_map[recipe_id] = {
                "name": name,
                "tags": tags.split(",") if tags else []
            }
        
        # Fetch ingredient names for better readability
        cursor = conn.execute("SELECT id, name FROM ingredients")
        ingredient_name_map = {}
        for row in cursor.fetchall():
            ingredient_id, name = row
            ingredient_name_map[ingredient_id] = name
            
        # Filter recipes by dietary preferences if specified
        if dietary_preferences:
            filtered_recipes = []
            for recipe in recipes:
                tags = [tag.strip() for tag in recipe["tags"]]
                if any(pref.lower() in [t.lower() for t in tags] for pref in dietary_preferences):
                    filtered_recipes.append(recipe)
            recipes = filtered_recipes
        
        # If no recipes are left after filtering, include all
        if not recipes:
            recipes = [r for r in recipes if r["name"] != ""]
        
        # Get recipe combinations
        combinations_map = {}
        cursor = conn.execute("SELECT id, name, description FROM recipe_combinations")
        for row in cursor.fetchall():
            combination_id, name, description = row
            combinations_map[combination_id] = {
                "name": name,
                "description": description,
                "recipes": []
            }
        
        # Fill in recipes for each combination  
        cursor = conn.execute("""
            SELECT rc.id, rc.recipe_id, r.name, rc.position 
            FROM recipe_combination_items rc
            JOIN recipes r ON rc.recipe_id = r.id
            ORDER BY rc.combination_id, rc.position
        """)
        
        for row in cursor.fetchall():
            combination_id, recipe_id, recipe_name, position = row
            if combination_id in combinations_map:
                combinations_map[combination_id]["recipes"].append({
                    "id": recipe_id,
                    "name": recipe_name,
                    "position": position
                })
        
        # Generate a balanced plan trying to avoid repetition of key ingredients/food groups
        meals = []
        used_recipes = []
        ingredient_usage_count = defaultdict(int)
        food_group_counts = defaultdict(int)  # Track how much of each food group we use
        
        base_food_groups = ["protein", "base", "veggie", "sauce", "pickle"]
        
        for i in range(num_days):
            date = start_date_obj + timedelta(days=i)
            
            # Try to avoid using the same recipes repeatedly
            available_recipes = [r for r in recipes if r["id"] not in used_recipes]
            
            if not available_recipes:
                # If all recipes have been used, reset the list
                available_recipes = recipes[:]
                used_recipes = []
                
            selected_recipe = None
            
            # Prioritize using combinations if available and matching dietary constraints
            if combinations_map:
                # Try to find a combination that includes one of our available recipes 
                candidate_combinations = [c for c in combinations_map.values() 
                                       if len(c["recipes"]) > 0]
                
                if candidate_combinations:
                    # Select a random combination
                    selected_combination = random.choice(candidate_combinations)
                    
                    # Try to use a recipe from the combination
                    available_from_combo = [r for r in selected_combination["recipes"] 
                                          if r["id"] in [r["id"] for r in available_recipes]]
                    
                    if available_from_combo:
                        selected_recipe = random.choice(available_from_combo)
                        
            # If we don't have a good combination, pick any available recipe
            if selected_recipe is None:
                selected_recipe = random.choice(available_recipes)
                
            # Update our bookkeeping to track usage
            used_recipes.append(selected_recipe["id"])
            for ingredient_id in selected_recipe["ingredient_ids"]:
                ingredient_usage_count[ingredient_id] += 1
                
            meals.append({
                "date": date.isoformat(),
                "day_of_week": date.strftime("%A"),
                "recipe": {
                    "id": selected_recipe["id"],
                    "name": selected_recipe["name"]
                },
                "meal_plan": {
                    "components": ["protein", "base", "veggie"],
                    "ingredients": selected_recipe["ingredient_ids"],
                    "ingredient_names": [ingredient_name_map.get(ing_id, f"Ingredient {ing_id}") 
                                       for ing_id in selected_recipe["ingredient_ids"]]
                }
            })
            
        plan["meals"] = meals
        
        # Write JSON output
        output_path.write_text(json.dumps(plan, indent=2) + "\n", encoding="utf-8")

        # Write markdown summary
        lines = [
            "# Weekly Meal Plan",
            "",
            f"Generated by: weekly_meal_planner.py",
            f"Start Date: {start_date_obj.strftime('%Y-%m-%d')}",
            f"Duration: {num_days} days",
            "",
        ]
        
        if dietary_preferences:
            lines.append(f"Dietary Preferences: {', '.join(dietary_preferences)}")
            lines.append("")
            
        if seasonal_constraints:
            lines.append(f"Seasonal Constraints: {', '.join(seasonal_constraints)}")
            lines.append("")
        
        for meal in plan["meals"]:
            lines.append(f"## {meal['date']} - {meal['day_of_week']}")
            lines.append(f"- **Recipe**: {meal['recipe']['name']}")
            
            # Add ingredients for better context
            if "ingredient_names" in meal["meal_plan"] and meal["meal_plan"]["ingredient_names"]:
                lines.append(f"- **Ingredients**: {', '.join(meal['meal_plan']['ingredient_names'])}")
            lines.append("")
            
        markdown_path.write_text("\n".join(lines).rstrip() + "\n", encoding="utf-8")

    finally:
        conn.close()

    return output_path, markdown_path


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Generate a weekly meal plan from available recipes.")
    parser.add_argument(
        "--food-dir",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "food",
        help="Path to the food directory to scan",
    )
    parser.add_argument(
        "--start-date",
        type=str,
        default=None,
        help="Start date for the plan (YYYY-MM-DD format)",
    )
    parser.add_argument(
        "--num-days",
        type=int,
        default=7,
        help="Number of days to plan for",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=None,
        help="Optional output path for the generated JSON plan",
    )
    parser.add_argument(
        "--markdown-output",
        type=Path,
        default=None,
        help="Optional output path for the generated markdown plan",
    )
    parser.add_argument(
        "--dietary-preferences",
        nargs="+",
        default=[],
        help="Dietary preferences to consider (vegetarian, high-protein, etc.)",
    )
    parser.add_argument(
        "--seasonal-constraints",
        nargs="+",
        default=[],
        help="Seasonal constraints to consider",
    )
    args = parser.parse_args()

    output_path, markdown_path = generate_weekly_plan(
        args.food_dir,
        args.start_date,
        args.num_days,
        args.output,
        args.markdown_output,
        args.dietary_preferences,
        args.seasonal_constraints,
    )
    print(f"Weekly meal plan written to {output_path}")
    if markdown_path is not None:
        print(f"Weekly meal plan markdown written to {markdown_path}")