import importlib.util
import json
from pathlib import Path
import sys
from urllib import request
from urllib.error import URLError

import pytest


SRC_ROOT = Path(__file__).resolve().parents[3]
EMBEDDING_PATH = SRC_ROOT / "ai" / "Embedding.Python" / "embedding_service.py"
OLLAMA_BASE_URL = "http://localhost:11434"
OLLAMA_MODEL = "nomic-embed-text"


def ollama_has_embedding_model_or_skip():
    try:
        with request.urlopen(f"{OLLAMA_BASE_URL}/api/tags", timeout=2) as response:
            tags = json.loads(response.read().decode("utf-8"))
    except (TimeoutError, URLError) as error:
        pytest.skip(f"Local Ollama is unavailable: {error}")

    names = {model.get("name", "").split(":")[0] for model in tags.get("models", [])}
    if OLLAMA_MODEL not in names:
        pytest.skip(f"Ollama model {OLLAMA_MODEL} is not installed")


def load_embedding_service_module():
    spec = importlib.util.spec_from_file_location("embedding_service", EMBEDDING_PATH)
    if spec is None or spec.loader is None:
        raise ImportError(f"Unable to load {EMBEDDING_PATH}")

    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def test_ollama_embedding_service_generates_vector_when_available():
    ollama_has_embedding_model_or_skip()
    module = load_embedding_service_module()

    response = module.OllamaEmbeddingService(base_url=OLLAMA_BASE_URL, model=OLLAMA_MODEL).generate_embedding(
        "Chicken curry with rice"
    )

    assert response.model == OLLAMA_MODEL
    assert response.dimensions > 0
    assert len(response.embedding) == response.dimensions