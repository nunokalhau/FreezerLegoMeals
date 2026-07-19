from __future__ import annotations

import argparse
import importlib.util
from pathlib import Path
import sys

EMBEDDINGS_ROOT = Path(__file__).resolve().parent
SRC_ROOT = EMBEDDINGS_ROOT.parents[1]
REPO_ROOT = SRC_ROOT.parent
REPOSITORY_PATH = SRC_ROOT / "repositories" / "Repository.Python" / "__init__.py"
PYTHON_EMBEDDING_SERVICE_PATH = SRC_ROOT / "ai" / "Embedding.Python" / "embedding_service.py"

if str(EMBEDDINGS_ROOT) not in sys.path:
    sys.path.insert(0, str(EMBEDDINGS_ROOT))

from embedding_indexer import EmbeddingIndexer
from recipe_embedding_builder import RecipeEmbeddingBuilder


def load_module(module_name: str, path: Path):
    spec = importlib.util.spec_from_file_location(module_name, path)
    if spec is None or spec.loader is None:
        raise ImportError(f"Unable to load {module_name} from {path}")

    module = importlib.util.module_from_spec(spec)
    sys.modules[module_name] = module
    spec.loader.exec_module(module)
    return module


def load_repository(db_path: Path | None = None):
    module = load_module("repository_python", REPOSITORY_PATH)
    return module.Repository(db_path)


def load_recipes(repository) -> list[dict]:
    recipes = repository.get_all_recipes_with_details()
    for recipe in recipes:
        recipe["recipe_ingredients"] = repository.get_recipe_ingredients(recipe["id"])
    return recipes


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate local recipe embedding files.")
    parser.add_argument("--db-path", type=Path, default=None)
    parser.add_argument("--output-dir", type=Path, default=REPO_ROOT / "data" / "embeddings")
    parser.add_argument("--ollama-url", default=None)
    parser.add_argument("--model", default=None)
    args = parser.parse_args()

    embedding_module = load_module("embedding_python_service", PYTHON_EMBEDDING_SERVICE_PATH)
    repository = load_repository(args.db_path)
    recipes = load_recipes(repository)
    indexer = EmbeddingIndexer(
        embedding_module.OllamaEmbeddingService(base_url=args.ollama_url, model=args.model),
        RecipeEmbeddingBuilder(),
    )
    count = indexer.index_recipes(recipes, args.output_dir)
    print(f"Generated {count} recipe embeddings in {args.output_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())