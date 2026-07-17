import json
import tempfile
import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src" / "scripts"))

from validate_recipe_structure import validate_recipe_structure


class ValidateRecipeStructureTests(unittest.TestCase):
    def test_validate_recipe_structure_accepts_well_formed_recipe(self):
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            food_dir = root / "src" / "food"
            proteins_dir = food_dir / "proteins"
            proteins_dir.mkdir(parents=True)

            recipe_path = proteins_dir / "chicken.md"
            recipe_path.write_text(
                "# Chicken\n\n"
                "**Tags:** high-protein, freezer-friendly\n"
                "**Tempo:** ~30 min\n"
                "**Porções:** 1\n\n"
                "---\n\n"
                "## 🧾 Ingredientes\n\n"
                "* 1 chicken breast\n\n"
                "## 👨‍🍳 Preparação\n\n"
                "1. Cook the chicken\n\n"
                "## ❄️ Congelação\n\n"
                "* Freeze after cooling\n\n"
                "## 🔥 Reaquecimento\n\n"
                "* Microwave\n\n"
                "## 🔄 Combinações\n\n"
                "* With rice\n\n"
                "## 💡 Notas\n\n"
                "* Keep it simple\n",
                encoding="utf-8",
            )

            json_output_path = food_dir / "recipe_validation.json"
            markdown_output_path = food_dir / "recipe_validation.md"
            result_paths = validate_recipe_structure(food_dir, json_output_path, markdown_output_path)

            self.assertEqual(result_paths[0], json_output_path)
            self.assertEqual(result_paths[1], markdown_output_path)
            self.assertTrue(json_output_path.exists())
            self.assertTrue(markdown_output_path.exists())

            payload = json.loads(json_output_path.read_text(encoding="utf-8"))
            self.assertEqual(payload["generated_by"], "validate_recipe_structure.py")
            self.assertEqual(payload["summary"]["valid_recipes"], 1)
            self.assertEqual(payload["summary"]["invalid_recipes"], 0)
            self.assertTrue(payload["recipes"][0]["valid"])
            self.assertEqual(payload["recipes"][0]["issues"], [])

    def test_validate_recipe_structure_reports_missing_sections(self):
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            food_dir = root / "src" / "food"
            proteins_dir = food_dir / "proteins"
            proteins_dir.mkdir(parents=True)

            recipe_path = proteins_dir / "broken.md"
            recipe_path.write_text(
                "# Broken Recipe\n\n"
                "**Tempo:** ~10 min\n\n"
                "## 🧾 Ingredientes\n\n"
                "* 1 item\n",
                encoding="utf-8",
            )

            validate_recipe_structure(food_dir)
            payload = json.loads((food_dir / "recipe_validation.json").read_text(encoding="utf-8"))

            self.assertEqual(payload["summary"]["valid_recipes"], 0)
            self.assertEqual(payload["summary"]["invalid_recipes"], 1)
            self.assertFalse(payload["recipes"][0]["valid"])
            self.assertTrue(any("missing metadata" in issue for issue in payload["recipes"][0]["issues"]))
            self.assertTrue(any("missing required section" in issue for issue in payload["recipes"][0]["issues"]))


if __name__ == "__main__":
    unittest.main()
