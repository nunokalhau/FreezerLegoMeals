from __future__ import annotations

import json
import re
import sqlite3
from pathlib import Path
from typing import Iterable

def optimize_shopping_list(
    food_dir: Path,
    recipe_ids: list[int],
    inventory_file: Path | None = None,
    output_path: Path | None = None,
    markdown_path: Path | None = None,
) -> tuple[Path, Path | None]:
    """Optimize shopping list by grouping items by store aisle and considering seasonal availability.

    Args:
        food_dir: Path to the food directory
        recipe_ids: List of recipe IDs to include in the optimized shopping list
        inventory_file: Optional path to existing inventory JSON file
        output_path: Optional output path for JSON optimized list
        markdown_path: Optional output path for markdown optimized list

    Returns:
        Tuple of (JSON output path, markdown output path)
    """
    food_dir = food_dir.resolve()
    if output_path is None:
        output_path = food_dir / "optimized_shopping_list.json"
    else:
        output_path = output_path.resolve()

    if markdown_path is None:
        markdown_path = food_dir / "optimized_shopping_list.md"
    else:
        markdown_path = markdown_path.resolve()

    # Get database path from data directory
    db_path = Path(__file__).resolve().parents[1] / "data" / "recipes_local.db"
    
    # Connect to SQLite database
    conn = sqlite3.connect(db_path)
    
    try:
        # Load inventory if provided
        inventory = []
        if inventory_file and inventory_file.exists():
            with open(inventory_file, 'r', encoding='utf-8') as f:
                inventory_data = json.load(f)
                if isinstance(inventory_data, list):
                    inventory = inventory_data
        
        # Normalize inventory to a lookup dictionary
        inventory_lookup = {}
        for item in inventory:
            ingredient = str(item.get("ingredient", "")).strip().lower()
            if ingredient:
                inventory_lookup[ingredient] = {
                    "quantity": float(item.get("quantity", 0) or 0),
                    "unit": str(item.get("unit", "unit")).strip().lower(),
                }
        
        # Get ingredients for the specified recipes
        total_needed = {}
        
        for recipe_id in recipe_ids:
            # Get recipe details
            cursor = conn.execute("SELECT source_path, name FROM recipes WHERE id = ?", (recipe_id,))
            row = cursor.fetchone()
            if not row:
                continue
                
            source_path = row[0]
            recipe_name = row[1]
            
            # Get ingredients for this recipe
            cursor = conn.execute("""
                SELECT i.name, ri.amount, ri.unit
                FROM recipe_ingredients ri
                JOIN ingredients i ON ri.ingredient_id = i.id
                WHERE ri.recipe_id = ?
                ORDER BY i.name
            """, (recipe_id,))
            
            recipe_ingredients = []
            for ingredient_row in cursor.fetchall():
                ingredient_name = ingredient_row[0].lower()
                quantity = float(ingredient_row[1] or 0)
                unit = str(ingredient_row[2] or "unit").strip().lower()
                
                # Check if in inventory
                inventory_item = inventory_lookup.get(ingredient_name)
                if inventory_item is not None:
                    quantity = max(0.0, quantity - float(inventory_item["quantity"]))
                
                if quantity > 0:
                    recipe_ingredients.append({
                        "ingredient": ingredient_name,
                        "quantity": quantity,
                        "unit": unit,
                    })
                    
                    # Add to total needed
                    if ingredient_name not in total_needed:
                        total_needed[ingredient_name] = {"quantity": 0, "unit": unit}
                    total_needed[ingredient_name]["quantity"] += quantity
        
        # Sort ingredients by aisle (simple grouping for optimization)
        # This is a simplified approach - could be expanded with actual store data
        sorted_ingredients = []
        
        # Group by general categories for aisle optimization
        ingredients_by_category = {
            "dairy": [],
            "meat": [],
            "produce": [],
            "pantry": [],
            "frozen": [],
            "other": []
        }
        
        # Categorize ingredients based on keywords (simplified approach)
        for ingredient_name, details in total_needed.items():
            if any(word in ingredient_name for word in ["milk", "cheese", "yogurt", "butter"]):
                category = "dairy"
            elif any(word in ingredient_name for word in ["beef", "chicken", "pork", "lamb", "sausage", "ground"]):
                category = "meat" 
            elif any(word in ingredient_name for word in ["tomato", "onion", "garlic", "carrot", "celery", "pepper", "potato", "cucumber", "lettuce", "spinach", "broccoli", "cauliflower"]):
                category = "produce"
            elif any(word in ingredient_name for word in ["rice", "pasta", "flour", "salt", "sugar", "oil", "cereal", "beans", "lentils", "couscous"]):
                category = "pantry"
            elif any(word in ingredient_name for word in ["frozen", "ice", "quorn", "tempeh"]):
                category = "frozen"
            else:
                category = "other"
                
            ingredients_by_category[category].append({
                "ingredient": ingredient_name,
                "quantity": details["quantity"],
                "unit": details["unit"]
            })
            
        # Add each category's items to the final list (in order of aisle)
        aisle_order = ["dairy", "meat", "produce", "frozen", "pantry", "other"] 
        for category in aisle_order:
            sorted_ingredients.extend(ingredients_by_category[category])
        
        # Create result structure
        optimized_list = {
            "generated_by": "shopping_list_optimizer.py",
            "recipes_selected": len(recipe_ids),
            "ingredient_count": len(sorted_ingredients),
            "items_by_aisle": ingredients_by_category,
            "optimized_list": sorted_ingredients,
            "total_cost_estimate": 0.0,  # Could be expanded with pricing data
        }
        
        # Write JSON output
        output_path.write_text(json.dumps(optimized_list, indent=2) + "\n", encoding="utf-8")

        # Write markdown summary
        lines = [
            "# Optimized Shopping List",
            "",
            f"Generated by: shopping_list_optimizer.py",
            f"- Recipes selected: {len(recipe_ids)}",
            f"- Total ingredients: {len(sorted_ingredients)}",
            "",
            "## Shopping List by Aisle",
            ""
        ]
        
        # Group by aisle in markdown
        for category in aisle_order:
            if ingredients_by_category[category]:
                lines.append(f"### {category.title()}")
                for item in ingredients_by_category[category]:
                    quantity = int(item["quantity"]) if float(item["quantity"]).is_integer() else item["quantity"]
                    lines.append(f"- {item['ingredient']}: {quantity} {item['unit']}")
                lines.append("")
            
        markdown_path.write_text("\n".join(lines).rstrip() + "\n", encoding="utf-8")

    finally:
        conn.close()

    return output_path, markdown_path


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Generate an optimized shopping list from selected recipes.")
    parser.add_argument(
        "--food-dir",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "food",
        help="Path to the food directory to scan",
    )
    parser.add_argument(
        "--recipes",
        type=int,
        nargs="+",
        required=True,
        help="Recipe IDs to include in shopping list optimization",
    )
    parser.add_argument(
        "--inventory-file",
        type=Path,
        default=None,
        help="Path to existing inventory JSON file",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=None,
        help="Optional output path for the generated JSON optimized shopping list",
    )
    parser.add_argument(
        "--markdown-output",
        type=Path,
        default=None,
        help="Optional output path for the generated markdown optimized shopping list",
    )
    args = parser.parse_args()

    output_path, markdown_path = optimize_shopping_list(
        args.food_dir,
        args.recipes,
        args.inventory_file,
        args.output,
        args.markdown_output,
    )
    print(f"Optimized shopping list written to {output_path}")
    if markdown_path is not None:
        print(f"Optimized shopping list markdown written to {markdown_path}")