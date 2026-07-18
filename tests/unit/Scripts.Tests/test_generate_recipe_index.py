import json
import tempfile
import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src" / "scripts"))

from generate_recipe_index import build_recipe_index


class GenerateRecipeIndexTests(unittest.TestCase):
    def test_build_recipe_index_ignores_non_recipe_docs_and_writes_output(self):
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            food_dir = root / "src" / "food"
            proteins_dir = food_dir / "proteins"
            veggies_dir = food_dir / "veggies"
            guides_dir = food_dir / "guides"
            proteins_dir.mkdir(parents=True)
            veggies_dir.mkdir(parents=True)
            guides_dir.mkdir(parents=True)

            (proteins_dir / "chicken.md").write_text("# Chicken\n", encoding="utf-8")
            (proteins_dir / "README.md").write_text("# Proteins\n", encoding="utf-8")
            (veggies_dir / "broccoli.md").write_text("# Broccoli\n", encoding="utf-8")
            (veggies_dir / "templates").mkdir(parents=True)
            (veggies_dir / "templates" / "veggie_template.md").write_text(
                "# Template\n", encoding="utf-8"
            )
            (guides_dir / "meal_prep.md").write_text("# Guide\n", encoding="utf-8")

            json_output_path = food_dir / "recipe_index.json"
            markdown_output_path = food_dir / "recipe_index.md"
            result_paths = build_recipe_index(food_dir, json_output_path, markdown_output_path)

            self.assertEqual(result_paths[0], json_output_path)
            self.assertEqual(result_paths[1], markdown_output_path)
            self.assertTrue(json_output_path.exists())
            self.assertTrue(markdown_output_path.exists())

            payload = json.loads(json_output_path.read_text(encoding="utf-8"))
            self.assertEqual(payload["generated_by"], "generate_recipe_index.py")
            self.assertEqual(payload["categories"][0]["name"], "proteins")
            self.assertEqual(payload["categories"][1]["name"], "veggies")
            self.assertEqual(payload["categories"][0]["recipes"][0]["path"], "proteins/chicken.md")
            self.assertEqual(payload["categories"][0]["recipes"][0]["title"], "Chicken")
            self.assertEqual(payload["categories"][0]["recipes"][0]["filename"], "chicken.md")
            self.assertEqual(payload["categories"][0]["recipes"][0]["category"], "proteins")

            content = markdown_output_path.read_text(encoding="utf-8")
            self.assertIn("# Recipe Index", content)
            self.assertIn("## Proteins", content)
            self.assertIn("## Veggies", content)
            self.assertIn("- [Chicken](proteins/chicken.md)", content)
            self.assertIn("- [Broccoli](veggies/broccoli.md)", content)
            self.assertNotIn("meal_prep", content)
            self.assertNotIn("veggie_template", content)


if __name__ == "__main__":
    unittest.main()
