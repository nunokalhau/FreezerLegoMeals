import json
import sqlite3
import tempfile
import unittest
from unittest import mock
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "scripts" / "recipes"))

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

            db_path = root / "recipes_local.db"
            conn = sqlite3.connect(db_path)
            try:
                conn.execute(
                    """
                    CREATE TABLE recipes (
                      id INTEGER PRIMARY KEY,
                      name TEXT,
                      source_path TEXT,
                      tags TEXT,
                      servings INTEGER,
                      time_to_prepare TEXT,
                      prepping TEXT,
                      freezing_notes TEXT,
                      reheat_notes TEXT,
                      combinations TEXT,
                      notes TEXT
                    )
                    """
                )
                conn.execute(
                    "INSERT INTO recipes (id, name, source_path, tags, servings, time_to_prepare) VALUES (1, 'Chicken', 'proteins/chicken.md', 'high-protein', 1, '~30 min')"
                )
                conn.commit()
            finally:
                conn.close()

            json_output_path = food_dir / "recipe_validation.json"
            markdown_output_path = food_dir / "recipe_validation.md"
            real_connect = sqlite3.connect
            with mock.patch("validate_recipe_structure.sqlite3.connect", side_effect=lambda _ignored: real_connect(db_path)):
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

            db_path = root / "recipes_local.db"
            conn = sqlite3.connect(db_path)
            try:
                conn.execute(
                    """
                    CREATE TABLE recipes (
                      id INTEGER PRIMARY KEY,
                      name TEXT,
                      source_path TEXT,
                      tags TEXT,
                      servings INTEGER,
                      time_to_prepare TEXT,
                      prepping TEXT,
                      freezing_notes TEXT,
                      reheat_notes TEXT,
                      combinations TEXT,
                      notes TEXT
                    )
                    """
                )
                conn.execute(
                    "INSERT INTO recipes (id, name, source_path, time_to_prepare) VALUES (1, 'Broken Recipe', 'proteins/broken.md', '~10 min')"
                )
                conn.commit()
            finally:
                conn.close()

            real_connect = sqlite3.connect
            with mock.patch("validate_recipe_structure.sqlite3.connect", side_effect=lambda _ignored: real_connect(db_path)):
                validate_recipe_structure(food_dir)
            payload = json.loads((food_dir / "recipe_validation.json").read_text(encoding="utf-8"))

            self.assertEqual(payload["summary"]["valid_recipes"], 0)
            self.assertEqual(payload["summary"]["invalid_recipes"], 1)
            self.assertFalse(payload["recipes"][0]["valid"])
            self.assertTrue(any("missing metadata" in issue for issue in payload["recipes"][0]["issues"]))
            self.assertTrue(any("missing required section" in issue for issue in payload["recipes"][0]["issues"]))


if __name__ == "__main__":
    unittest.main()
