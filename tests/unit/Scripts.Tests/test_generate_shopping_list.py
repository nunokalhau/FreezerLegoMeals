import json
import tempfile
import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src" / "scripts"))

from generate_shopping_list import generate_shopping_list


class GenerateShoppingListTests(unittest.TestCase):
    def test_generate_shopping_list_uses_inventory_and_deduplicates_needed_items(self):
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            food_dir = root / "src" / "food"
            proteins_dir = food_dir / "proteins"
            proteins_dir.mkdir(parents=True)

            (proteins_dir / "chicken.md").write_text(
                "# Chicken\n\n"
                "**Tags:** high-protein\n"
                "**Tempo:** ~30 min\n"
                "**Porções:** 1\n\n"
                "---\n\n"
                "## 🧾 Ingredientes\n\n"
                "* 2 chicken breasts\n"
                "* 1 onion\n\n"
                "## 👨‍🍳 Preparação\n\n"
                "1. Cook the chicken\n",
                encoding="utf-8",
            )
            (proteins_dir / "beef.md").write_text(
                "# Beef\n\n"
                "**Tags:** high-protein\n"
                "**Tempo:** ~20 min\n"
                "**Porções:** 1\n\n"
                "---\n\n"
                "## 🧾 Ingredientes\n\n"
                "* 1 onion\n"
                "* 1 tomato\n\n"
                "## 👨‍🍳 Preparação\n\n"
                "1. Cook the beef\n",
                encoding="utf-8",
            )

            recipes_file = root / "recipes.json"
            recipes_file.write_text(json.dumps(["proteins/chicken.md", "proteins/beef.md"]), encoding="utf-8")
            inventory_file = root / "inventory.json"
            inventory_file.write_text(
                json.dumps(
                    [
                        {"ingredient": "onion", "unit": "unit", "quantity": 1},
                        {"ingredient": "chicken breasts", "unit": "unit", "quantity": 1},
                    ]
                ),
                encoding="utf-8",
            )

            json_output_path = food_dir / "shopping_list.json"
            markdown_output_path = food_dir / "shopping_list.md"
            result_paths = generate_shopping_list(
                food_dir,
                recipes_file,
                inventory_file,
                json_output_path,
                markdown_output_path,
            )

            self.assertEqual(result_paths[0], json_output_path)
            self.assertEqual(result_paths[1], markdown_output_path)
            self.assertTrue(json_output_path.exists())
            self.assertTrue(markdown_output_path.exists())

            payload = json.loads(json_output_path.read_text(encoding="utf-8"))
            self.assertEqual(payload["generated_by"], "generate_shopping_list.py")
            self.assertEqual(payload["summary"]["recipes_selected"], 2)
            self.assertEqual(payload["summary"]["ingredient_count"], 3)
            self.assertEqual(
                payload["required_ingredients"],
                [
                    {"ingredient": "chicken breasts", "unit": "unit", "quantity": 1},
                    {"ingredient": "onion", "unit": "unit", "quantity": 1},
                    {"ingredient": "tomato", "unit": "unit", "quantity": 1},
                ],
            )
            self.assertEqual(payload["recipes"][0]["path"], "proteins/chicken.md")
            self.assertEqual(payload["recipes"][1]["path"], "proteins/beef.md")

            content = markdown_output_path.read_text(encoding="utf-8")
            self.assertIn("# Shopping List", content)
            self.assertIn("- chicken breasts: 1 unit", content)
            self.assertIn("- onion: 1 unit", content)
            self.assertIn("- tomato: 1 unit", content)


if __name__ == "__main__":
    unittest.main()
