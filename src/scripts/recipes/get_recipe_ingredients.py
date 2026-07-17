from __future__ import annotations

import argparse
import sqlite3
import sys
from pathlib import Path

def get_recipe_ingredients(
    db_path: Path,
    recipe_identifier: str
) -> list[dict]:
    """
    Get all ingredients for a specified recipe by name or ID.
    
    Args:
        db_path: Path to the SQLite database
        recipe_identifier: Recipe name or ID
        
    Returns:
        List of ingredient dictionaries with name, amount, and unit
    """
    conn = sqlite3.connect(db_path)
    
    try:
        # Determine if the identifier is numeric (ID) or text (name)
        try:
            recipe_id = int(recipe_identifier)
            cursor = conn.execute("SELECT id, name FROM recipes WHERE id = ?", (recipe_identifier,))
            recipe_row = cursor.fetchone()
        except ValueError:
            # Not a number, search by name
            cursor = conn.execute("SELECT id, name FROM recipes WHERE name = ?", (recipe_identifier,))
            recipe_row = cursor.fetchone()
        
        if not recipe_row:
            return []
            
        recipe_id, recipe_name = recipe_row
        
        # Get ingredients for this recipe
        query = """
            SELECT i.name, ri.amount, ri.unit
            FROM recipe_ingredients ri
            JOIN ingredients i ON ri.ingredient_id = i.id
            WHERE ri.recipe_id = ?
            ORDER BY i.name
        """
        
        cursor = conn.execute(query, (recipe_id,))
        ingredients = []
        
        for row in cursor.fetchall():
            ingredient_name, amount, unit = row
            ingredients.append({
                "name": ingredient_name,
                "amount": amount,
                "unit": unit
            })
        
        return ingredients
        
    finally:
        conn.close()

def main():
    parser = argparse.ArgumentParser(
        description="Get ingredients for a specified recipe",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python get_recipe_ingredients.py --recipe "Chicken Curry"
  python get_recipe_ingredients.py -r 12
        """
    )
    
    parser.add_argument(
        "--recipe",
        "-r",
        required=True,
        help="Recipe name or ID to get ingredients for",
    )
    
    args = parser.parse_args()
    
    # Get database path from data directory
    db_path = Path(__file__).resolve().parents[3] / "data" / "recipes_local.db"
    
    if not db_path.exists():
        print(f"Error: Database not found at {db_path}")
        sys.exit(1)
    
    try:
        ingredients = get_recipe_ingredients(db_path, args.recipe)
        
        if not ingredients:
            print(f"No recipe found with name or ID: {args.recipe}")
            sys.exit(1)
            
        # Format and display results
        print(f"Ingredients for '{args.recipe}':")
        print("-" * 40)
        
        for ingredient in ingredients:
            amount_str = f"{ingredient['amount']} " if ingredient['amount'] else ""
            unit_str = f"{ingredient['unit']} " if ingredient['unit'] else ""
            print(f"{amount_str}{unit_str}{ingredient['name']}")
            
    except Exception as e:
        print(f"Error retrieving recipe ingredients: {str(e)}")
        sys.exit(1)

if __name__ == "__main__":
    main()