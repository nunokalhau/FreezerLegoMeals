from __future__ import annotations

import json
import re
import sqlite3
from pathlib import Path
from typing import Iterable

IGNORED_DIRECTORIES = {"guides", "templates"}
KNOWN_UNITS = {
    "g",
    "kg",
    "mg",
    "ml",
    "l",
    "cl",
    "cup",
    "cups",
    "c",
    "tbsp",
    "tsp",
    "pinch",
    "pinches",
    "oz",
    "lb",
    "lbs",
    "can",
    "cans",
    "package",
    "packages",
    "packet",
    "packets",
    "slice",
    "slices",
    "piece",
    "pieces",
    "unit",
    "units",
    "clove",
    "cloves",
    "bunch",
    "bunches",
    "handful",
    "handfuls",
    "sprig",
    "sprigs",
}

def _is_recipe_file(path: Path) -> bool:
    if not path.is_file():
        return False
    if path.name.lower() == "readme.md":
        return False
    if path.name.lower().endswith("_template.md"):
        return False
    if path.suffix.lower() != ".md":
        return False
    return True


def _iter_recipe_files(food_dir: Path) -> Iterable[Path]:
    for category_dir in sorted(food_dir.iterdir()):
        if not category_dir.is_dir():
            continue
        if category_dir.name.startswith("."):
            continue
        if category_dir.name.lower() in IGNORED_DIRECTORIES:
            continue
        for path in sorted(category_dir.iterdir()):
            if _is_recipe_file(path):
                yield path


def _extract_ingredients(recipe_path: Path) -> list[dict[str, object]]:
    content = recipe_path.read_text(encoding="utf-8")
    section_match = re.search(r"##\s+🧾\s+Ingredientes\s*(.*?)(?=\n\n##|\Z)", content, re.S)
    if not section_match:
        return []

    ingredients: list[dict[str, object]] = []
    for line in section_match.group(1).splitlines():
        stripped = line.strip()
        if not stripped or not stripped.startswith("* "):
            continue
        text = stripped[2:].strip()
        quantity, unit, ingredient = _infer_ingredient(text)
        ingredients.append(
            {
                "raw": text,
                "quantity": quantity,
                "unit": unit,
                "ingredient": ingredient,
            }
        )
    return ingredients


def _infer_ingredient(text: str) -> tuple[float, str, str]:
    match = re.match(r"^(?P<quantity>\d+(?:[.,]\d+)?)\s*(?P<rest>.+)?$", text)
    if not match:
        return 1.0, "unit", text.strip().lower()

    quantity = float(match.group("quantity").replace(",", "."))
    rest = (match.group("rest") or "").strip().lower()
    if not rest:
        return quantity, "unit", ""

    parts = rest.split()
    first_token = parts[0]
    if first_token in KNOWN_UNITS:
        unit = first_token
        ingredient = " ".join(parts[1:]).strip() if len(parts) > 1 else ""
        return quantity, unit, ingredient

    ingredient = rest
    return quantity, "unit", ingredient


def _load_json(path: Path) -> object:
    if not path.exists():
        return []
    return json.loads(path.read_text(encoding="utf-8"))


def _normalize_inventory(inventory: list[dict[str, object]]) -> dict[str, dict[str, object]]:
    normalized: dict[str, dict[str, object]] = {}
    for item in inventory:
        ingredient = str(item.get("ingredient", "")).strip().lower()
        if not ingredient:
            continue
        normalized[ingredient] = {
            "quantity": float(item.get("quantity", 0) or 0),
            "unit": str(item.get("unit", "unit")).strip().lower(),
        }
    return normalized


def generate_shopping_list(
    food_dir: Path,
    recipe_input: Path | list[str] | list[int] | None = None,
    inventory_input: Path | list[dict[str, object]] | None = None,
    output_path: Path | None = None,
    markdown_path: Path | None = None,
) -> tuple[Path, Path | None]:
    food_dir = food_dir.resolve()
    if output_path is None:
        output_path = food_dir / "shopping_list.json"
    else:
        output_path = output_path.resolve()

    if markdown_path is None:
        markdown_path = food_dir / "shopping_list.md"
    else:
        markdown_path = markdown_path.resolve()
    
    # Get database path from data directory
    db_path = Path(__file__).resolve().parents[3] / "data" / "recipes_local.db"
    
    # Connect to SQLite database
    conn = sqlite3.connect(db_path)
    
    try:
        if recipe_input is None:
            # Get all recipes with IDs from database
            cursor = conn.execute("SELECT id, source_path FROM recipes ORDER BY name")
            selected_paths = [row[0] for row in cursor.fetchall()]  # Using recipe ID instead of path
        elif isinstance(recipe_input, Path):
            # Load recipe IDs from a JSON file
            recipe_ids = _load_json(recipe_input)
            if isinstance(recipe_ids, list) and len(recipe_ids) > 0 and isinstance(recipe_ids[0], int):
                selected_paths = recipe_ids  # These are already recipe IDs
            else:
                # If it's a list of paths (older format), convert to IDs
                selected_paths = []
                for path_str in recipe_ids:
                    cursor = conn.execute("SELECT id FROM recipes WHERE source_path = ?", (path_str,))
                    row = cursor.fetchone()
                    if row:
                        selected_paths.append(row[0])
        else:
            selected_paths = list(recipe_input)  # This should contain recipe IDs if it's a list of integers

        if inventory_input is None:
            inventory = []
        elif isinstance(inventory_input, Path):
            inventory = _load_json(inventory_input)
        else:
            inventory = list(inventory_input)

        inventory_lookup = _normalize_inventory(inventory if isinstance(inventory, list) else [])

        recipes = []
        total_needed: dict[str, dict[str, object]] = {}
        
        # For each recipe ID in selected_paths
        for recipe_id in selected_paths:
            # Get recipe by ID from database
            cursor = conn.execute("SELECT source_path FROM recipes WHERE id = ?", (recipe_id,))
            row = cursor.fetchone()
            if not row:
                continue
            
            relative_path = row[0]
            recipe_path = food_dir / relative_path
            
            if not recipe_path.exists():
                continue
                
            parsed_ingredients = _extract_ingredients(recipe_path)
            recipes.append({"id": recipe_id, "path": relative_path, "ingredients": parsed_ingredients})
            
            for ingredient in parsed_ingredients:
                ingredient_name = str(ingredient["ingredient"])
                quantity = float(ingredient["quantity"] or 0)
                unit = str(ingredient["unit"] or "unit")
                existing = total_needed.get(ingredient_name)
                if existing is None:
                    total_needed[ingredient_name] = {"quantity": quantity, "unit": unit}
                else:
                    existing["quantity"] = float(existing["quantity"]) + quantity

        required_ingredients = []
        for ingredient_name, details in sorted(total_needed.items(), key=lambda item: item[0]):
            needed_quantity = float(details["quantity"])
            inventory_item = inventory_lookup.get(ingredient_name)
            if inventory_item is not None:
                needed_quantity = max(0.0, needed_quantity - float(inventory_item["quantity"]))
            if needed_quantity <= 0:
                continue
            required_ingredients.append(
                {
                    "ingredient": ingredient_name,
                    "unit": str(details["unit"]),
                    "quantity": needed_quantity,
                }
            )

        payload = {
            "generated_by": "generate_shopping_list.py",
            "food_dir": food_dir.as_posix(),
            "summary": {
                "recipes_selected": len(recipes),
                "ingredient_count": len(required_ingredients),
            },
            "recipes": recipes,
            "required_ingredients": required_ingredients,
        }

        output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

        lines = [
            "# Shopping List",
            "",
            "This file is generated by the shopping list generator.",
            "",
            f"- Recipes selected: {payload['summary']['recipes_selected']}",
            f"- Ingredient count: {payload['summary']['ingredient_count']}",
            "",
        ]
        for ingredient in required_ingredients:
            quantity = int(ingredient["quantity"]) if float(ingredient["quantity"]).is_integer() else ingredient["quantity"]
            lines.append(f"- {ingredient['ingredient']}: {quantity} {ingredient['unit']}")
        lines.append("")

        markdown_path.write_text("\n".join(lines).rstrip() + "\n", encoding="utf-8")

    finally:
        conn.close()

    return output_path, markdown_path


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Generate a shopping list from selected recipes and optional inventory data.")
    parser.add_argument(
        "--food-dir",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "food",
        help="Path to the food directory to scan",
    )
    parser.add_argument(
        "--recipes-file",
        type=Path,
        default=None,
        help="Path to a JSON file containing recipe paths relative to the food directory",
    )
    parser.add_argument(
        "--inventory-file",
        type=Path,
        default=None,
        help="Path to a JSON file containing inventory items with ingredient, unit, and quantity",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=None,
        help="Optional output path for the generated JSON shopping list",
    )
    parser.add_argument(
        "--markdown-output",
        type=Path,
        default=None,
        help="Optional output path for the generated markdown shopping list",
    )
    args = parser.parse_args()

    output_path, markdown_path = generate_shopping_list(
        args.food_dir,
        args.recipes_file,
        args.inventory_file,
        args.output,
        args.markdown_output,
    )
    print(f"Shopping list written to {output_path}")
    if markdown_path is not None:
        print(f"Shopping list markdown written to {markdown_path}")
