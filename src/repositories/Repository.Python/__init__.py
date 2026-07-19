"""
Repository layer for Freezer Lego Meals.

This is the only layer that directly accesses:
- SQLite database 
- Markdown recipe files
- recipe_index
- Any future storage

All other layers must go through this repository layer to access data.
"""

from pathlib import Path
from typing import List, Dict, Optional, Any
import sqlite3
import json


class Repository:
    """Centralized data access layer."""
    
    def __init__(self, db_path: Optional[Path] = None):
        if db_path is None:
            # Look relative to this file for the database path
            self.db_path = Path(__file__).resolve().parents[2] / "data" / "recipes_local.db"
        else:
            self.db_path = db_path.resolve()
    
    def get_recipe_by_id(self, recipe_id: int) -> Optional[Dict[str, Any]]:
        """Get a specific recipe by ID."""
        if not self.db_path.exists():
            return None
            
        conn = sqlite3.connect(self.db_path)
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

    def get_recipes(self) -> List[Dict[str, Any]]:
        """Get all recipes with details (DotNet parity method name)."""
        return self.get_all_recipes_with_details()

    def search_recipes_by_ingredients(self, ingredients: List[str]) -> List[Dict[str, Any]]:
        """Search for recipes containing specified ingredients."""
        if not self.db_path.exists():
            return []
            
        conn = sqlite3.connect(self.db_path)
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

    def get_all_recipes(self) -> List[Dict[str, Any]]:
        """Get all recipes in the database."""
        if not self.db_path.exists():
            return []
            
        conn = sqlite3.connect(self.db_path)
        try:
            query = """
                SELECT id, name, source_path
                FROM recipes
                ORDER BY name
            """
            cursor = conn.execute(query)
            recipes = []
            
            for row in cursor.fetchall():
                recipe_id, name, source_path = row
                recipes.append({
                    "id": recipe_id,
                    "name": name,
                    "source_path": source_path
                })
            
            return recipes
        finally:
            conn.close()

    def get_recipe_ingredients(self, recipe_id: int) -> List[Dict[str, Any]]:
        """Get all ingredients for a specific recipe."""
        if not self.db_path.exists():
            return []
            
        conn = sqlite3.connect(self.db_path)
        try:
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

    def get_all_ingredients(self) -> Dict[int, str]:
        """Get mapping of all ingredients."""
        if not self.db_path.exists():
            return {}
            
        conn = sqlite3.connect(self.db_path)
        try:
            query = "SELECT id, name FROM ingredients ORDER BY name"
            cursor = conn.execute(query)
            return {row[0]: row[1] for row in cursor.fetchall()}
        finally:
            conn.close()

    def get_ingredients(self) -> List[Dict[str, Any]]:
        """Get all ingredients as objects (DotNet parity method name)."""
        ingredients = self.get_all_ingredients()
        return [{"id": ingredient_id, "name": name} for ingredient_id, name in ingredients.items()]

    def get_recipe_combinations(self) -> Dict[int, Dict[str, Any]]:
        """Get recipe combinations data."""
        if not self.db_path.exists():
            return {}
            
        conn = sqlite3.connect(self.db_path)
        try:
            combinations_map = {}
            cursor = conn.execute("SELECT id, name, description FROM recipe_combinations")
            for row in cursor.fetchall():
                combination_id, name, description = row
                combinations_map[combination_id] = {
                    "name": name,
                    "description": description,
                    "recipes": []
                }
            
            # Get combination items
            cursor = conn.execute("""
                SELECT rci.combination_id, rci.recipe_id, r.name, rci.position
                FROM recipe_combination_items rci
                JOIN recipes r ON rci.recipe_id = r.id
                ORDER BY rci.combination_id, rci.position
            """)
            
            for row in cursor.fetchall():
                combination_id, recipe_id, recipe_name, position = row
                if combination_id in combinations_map:
                    combinations_map[combination_id]["recipes"].append({
                        "id": recipe_id,
                        "name": recipe_name,
                        "position": position
                    })
            
            return combinations_map
        finally:
            conn.close()

    def get_combinations(self) -> List[Dict[str, Any]]:
        """Get all combinations as a list (DotNet parity method name)."""
        combinations_map = self.get_recipe_combinations()
        return [
            {
                "id": combination_id,
                "name": data.get("name", ""),
                "description": data.get("description", ""),
                "recipes": data.get("recipes", [])
            }
            for combination_id, data in combinations_map.items()
        ]

    def get_combination_by_id(self, combination_id: int) -> Optional[Dict[str, Any]]:
        """Get a specific combination by ID (DotNet parity method name)."""
        if combination_id <= 0:
            return None

        combinations_map = self.get_recipe_combinations()
        if combination_id not in combinations_map:
            return None

        data = combinations_map[combination_id]
        return {
            "id": combination_id,
            "name": data.get("name", ""),
            "description": data.get("description", ""),
            "recipes": data.get("recipes", [])
        }

    def get_ingredient_by_name(self, name: str) -> Optional[Dict[str, Any]]:
        """Get a specific ingredient by name (DotNet parity method name)."""
        if not self.db_path.exists() or not name or not name.strip():
            return None

        conn = sqlite3.connect(self.db_path)
        try:
            cursor = conn.execute(
                "SELECT id, name FROM ingredients WHERE lower(name) = lower(?) LIMIT 1",
                (name.strip(),)
            )
            row = cursor.fetchone()
            if not row:
                return None

            return {
                "id": row[0],
                "name": row[1]
            }
        finally:
            conn.close()

    def get_recipe_details(self, recipe_identifier: str) -> List[Dict[str, Any]]:
        """Get recipe details by name or ID."""
        if not self.db_path.exists():
            return []
            
        conn = sqlite3.connect(self.db_path)
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
                
            return [self.get_recipe_by_id(recipe_row[0])]
        finally:
            conn.close()

    def get_all_recipes_with_details(self) -> List[Dict[str, Any]]:
        """Get all recipes with detailed information."""
        if not self.db_path.exists():
            return []
            
        conn = sqlite3.connect(self.db_path)
        try:
            cursor = conn.execute("""
                SELECT r.id, r.name, r.source_path, r.tags, r.servings, 
                       r.time_to_prepare, r.prepping, r.freezing_notes, 
                       r.reheat_notes, r.combinations, r.notes
                FROM recipes r
                ORDER BY name
            """)
            
            recipes = []
            for row in cursor.fetchall():
                recipe_id, name, source_path, tags, servings, time_to_prepare, \
                    prepping, freezing_notes, reheat_notes, combinations, notes = row
                
                recipes.append({
                    "id": recipe_id,
                    "name": name,
                    "source_path": source_path,
                    "tags": tags.split(",") if tags else [],
                    "servings": servings or 1,
                    "time_to_prepare": time_to_prepare or 0,
                    "prepping": prepping,
                    "freezing_notes": freezing_notes,
                    "reheat_notes": reheat_notes,
                    "combinations": combinations.split(",") if combinations else [],
                    "notes": notes
                })
            
            return recipes
        finally:
            conn.close()