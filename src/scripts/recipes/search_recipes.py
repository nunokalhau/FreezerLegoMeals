from __future__ import annotations

import argparse
import json
import sqlite3
import sys
from pathlib import Path
from typing import List

def search_recipes_by_ingredients(
    db_path: Path,
    ingredients: List[str],
) -> List[dict]:
    """
    Search for recipes containing any of the specified ingredients.
    
    Args:
        db_path: Path to the SQLite database
        ingredients: List of ingredient names to search for
        
    Returns:
        List of recipe dictionaries with matching ingredients
    """
    conn = sqlite3.connect(db_path)
    
    try:
        # Build the query with OR logic for ingredients
        ingredient_placeholders = ", ".join(["?" for _ in ingredients])
        query = f"""
            SELECT DISTINCT r.id, r.name, r.source_path, 
                   GROUP_CONCAT(i.name) as matched_ingredients
            FROM recipes r
            JOIN recipe_ingredients ri ON r.id = ri.recipe_id
            JOIN ingredients i ON ri.ingredient_id = i.id
            WHERE i.name IN ({ingredient_placeholders})
            GROUP BY r.id, r.name, r.source_path
            ORDER BY r.name
        """
        
        cursor = conn.execute(query, ingredients)
        recipes = []
        
        for row in cursor.fetchall():
            recipe_id, name, source_path, matched_ingredients_str = row
            
            # Split the matched ingredients if they exist
            matched_ingredients = matched_ingredients_str.split(",") if matched_ingredients_str else []
            
            recipes.append({
                "id": recipe_id,
                "name": name,
                "source_path": source_path,
                "matched_ingredients": matched_ingredients
            })
        
        return recipes
    
    finally:
        conn.close()


def search_recipe_by_id(
    db_path: Path,
    recipe_id: int
) -> dict:
    """
    Search for a specific recipe by ID.
    
    Args:
        db_path: Path to the SQLite database
        recipe_id: Recipe ID to search for
        
    Returns:
        Recipe dictionary if found, None otherwise
    """
    conn = sqlite3.connect(db_path)
    
    try:
        query = """
            SELECT r.id, r.name, r.source_path, r.tags, r.servings, 
                   r.time_to_prepare, r.prepping, r.freezing_notes, 
                   r.reheat_notes, r.combinations, r.notes
            FROM recipes r
            WHERE r.id = ?
            ORDER BY r.name
        """
        
        cursor = conn.execute(query, (recipe_id,))
        row = cursor.fetchone()
        
        if not row:
            return None
        
        recipe_id, name, source_path, tags, servings, time_to_prepare, prepping, freezing_notes, reheat_notes, combinations, notes = row
        
        return {
            "id": recipe_id,
            "name": name,
            "source_path": source_path,
            "tags": tags,
            "servings": servings,
            "time_to_prepare": time_to_prepare,
            "prepping": prepping,
            "freezing_notes": freezing_notes,
            "reheat_notes": reheat_notes,
            "combinations": combinations,
            "notes": notes
        }
    
    finally:
        conn.close()


def main():
    parser = argparse.ArgumentParser(
        description="Search for recipes by ingredients or ID",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python search_recipes.py --ingredients "carne de vaca" "frango"
  python search_recipes.py -i "tofu firme" "cebola" "alho"
  python search_recipes.py --recipe-id 123
        """
    )
    
    parser.add_argument(
        "--ingredients",
        "-i",
        nargs="+",
        help="Ingredients to search for (OR logic)",
    )
    
    parser.add_argument(
        "--recipe-id",
        type=int,
        help="Search for a specific recipe by ID",
    )
    
    args = parser.parse_args()
    
    # Get database path from data directory
    db_path = Path(__file__).resolve().parents[3] / "data" / "recipes_local.db"
    
    if not db_path.exists():
        print(f"Error: Database not found at {db_path}")
        sys.exit(1)
    
    try:
        # Handle search by ID or by ingredients
        if args.recipe_id is not None:
            # Search by recipe ID
            result = search_recipe_by_id(db_path, args.recipe_id)
            
            if not result:
                print(f"No recipe found with ID: {args.recipe_id}")
                sys.exit(1)
                
            formatted_result = {
                "search_type": "id",
                "recipe_id": args.recipe_id,
                "recipe": result
            }
            
            print(json.dumps(formatted_result, indent=2))
        elif args.ingredients is not None:
            # Search by ingredients
            results = search_recipes_by_ingredients(db_path, args.ingredients)
            
            # Format and output results
            formatted_results = {
                "search_terms": args.ingredients,
                "total_recipes_found": len(results),
                "recipes": results
            }
            
            print(json.dumps(formatted_results, indent=2))
        else:
            parser.print_help()
            sys.exit(1)
        
    except Exception as e:
        print(f"Error searching for recipes: {str(e)}")
        sys.exit(1)


if __name__ == "__main__":
    main()