import json
import tempfile
import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "scripts" / "recipes"))

from generate_localization_seed import build_seed_sql, check_seed, generate_seed


class GenerateLocalizationSeedTests(unittest.TestCase):
    def test_generate_seed_is_deterministic_and_check_detects_drift(self):
        metadata = {
            "recipe_id": 2,
            "updated_at_utc": "2026-08-01T00:00:00Z",
            "provenance": "phase3-seed-generator",
            "recipe_translations": [
                {
                    "language": "en",
                    "name": "Salsa Verde Chicken",
                    "tags": "high-protein, freezer-friendly",
                    "notes": "Fresh and versatile chicken base for meal prep.",
                    "prepping": "Cook chicken with tomato, onion, garlic, chili, then finish with cilantro and lime.",
                    "translation_version": 1,
                },
                {
                    "language": "pt",
                    "name": "Frango Salsa Verde",
                    "tags": "alto-proteina, congelador",
                    "notes": "Base fresca e versatil para meal prep.",
                    "prepping": "Cozinhar o frango com tomate, cebola, alho e malagueta, terminar com coentros e lima.",
                    "translation_version": 1,
                },
            ],
            "ingredient_translations": [
                {"ingredient_id": 9, "language": "en", "name": "chicken thigh", "unit": "piece", "translation_version": 1},
                {"ingredient_id": 9, "language": "pt", "name": "frango", "unit": "g", "translation_version": 1},
            ],
            "index_metadata": {
                "projection_schema_version": "phase3-localized-seed-v1",
                "projection_fingerprint": "phase3-salsa-verde-seed",
                "language_coverage": ["en", "pt"],
            },
        }

        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            metadata_path = root / "salsa.localization.json"
            output_path = root / "salsa.localization.seed.sql"

            metadata_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")

            first = generate_seed(metadata_path, output_path)
            second = generate_seed(metadata_path, output_path)

            self.assertEqual(first, second)
            self.assertTrue(check_seed(metadata_path, output_path))

            output_path.write_text(first + "\n-- drift", encoding="utf-8")
            self.assertFalse(check_seed(metadata_path, output_path))

    def test_build_seed_sql_contains_expected_tables(self):
        metadata = {
            "recipe_id": 2,
            "updated_at_utc": "2026-08-01T00:00:00Z",
            "recipe_translations": [{"language": "en", "name": "Salsa Verde Chicken"}],
            "ingredient_translations": [{"ingredient_id": 9, "language": "en", "name": "chicken thigh"}],
        }

        sql = build_seed_sql(metadata)

        self.assertIn("INSERT OR REPLACE INTO recipe_translations", sql)
        self.assertIn("INSERT OR REPLACE INTO ingredient_translations", sql)
        self.assertIn("INSERT OR REPLACE INTO recipe_index_metadata", sql)


if __name__ == "__main__":
    unittest.main()
