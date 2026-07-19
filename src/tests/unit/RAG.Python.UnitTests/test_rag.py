import importlib.util
from pathlib import Path
import sys
from types import SimpleNamespace


SRC_ROOT = Path(__file__).resolve().parents[3]
RAG_PATH = SRC_ROOT / "ai" / "RAG" / "Python"
ASSISTANT_PATH = SRC_ROOT / "services" / "Services.Python" / "assistant_service.py"


def load_module(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise ImportError(f"Unable to load {path}")

    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


retrieval_module = load_module("rag_python_retrieval_service", RAG_PATH / "retrieval_service.py")
prompt_module = load_module("rag_python_prompt_builder", RAG_PATH / "prompt_builder.py")
assistant_module = load_module("services_python_assistant_rag_tests", ASSISTANT_PATH)


class StubSemanticSearchService:
    def __init__(self, results):
        self.results = results

    def search(self, query, top_k):
        return self.results[:top_k]


class StubMetadataProvider:
    def get_metadata(self, recipe_id):
        return {
            "id": recipe_id,
            "name": "Spicy Chicken",
            "notes": "Freezer-friendly chicken dinner",
            "tags": ["spicy", "chicken"],
            "recipe_ingredients": [{"name": "chicken"}, {"name": "pepper"}],
            "prepping": "Slice chicken and season it",
            "time_to_prepare": 45,
        }


class StubConversationStore:
    def __init__(self):
        self.persisted = []

    def get_or_create_conversation(self, conversation_id=None):
        return SimpleNamespace(conversation_id=conversation_id or "conversation-1", messages=[])

    def append_messages(self, conversation_id, messages):
        self.persisted.append((conversation_id, messages))


class StubToolExecutor:
    def __init__(self):
        self.executed = False

    def get_tools(self):
        return [{"name": "search_recipes", "description": "Search recipes", "parameters": ["query"]}]

    def execute(self, name, arguments):
        self.executed = True
        return {"success": True, "tool": name, "recipes": []}


class StubOllamaClient:
    def __init__(self, first_result, rag_content="Use the spicy chicken recipe."):
        self.first_result = first_result
        self.rag_content = rag_content
        self.calls = []

    def chat(self, model, messages, tools=None):
        self.calls.append({"messages": messages, "tools": tools or []})
        if len(self.calls) == 1:
            return self.first_result
        return SimpleNamespace(content=self.rag_content, tool_calls=[])


def test_retrieval_service_returns_structured_context_and_sources():
    result = SimpleNamespace(recipeId="1", title="Spicy Chicken", score=0.91)
    service = retrieval_module.RetrievalService(StubSemanticSearchService([result]), StubMetadataProvider(), top_k=3)

    retrieval = service.retrieve("What spicy chicken meal can I cook?")

    assert len(retrieval.recipes) == 1
    assert retrieval.recipes[0].ingredients == ["chicken", "pepper"]
    assert retrieval.recipes[0].preparationSteps == "Slice chicken and season it"
    assert retrieval.sources[0].recipeId == "1"
    assert retrieval.sources[0].similarityScore == 0.91


def test_retrieval_service_filters_empty_or_low_similarity_results():
    result = SimpleNamespace(recipeId="1", title="Spicy Chicken", score=0.01)
    service = retrieval_module.RetrievalService(StubSemanticSearchService([result]), StubMetadataProvider(), minimum_similarity=0.2)

    retrieval = service.retrieve("Unknown question")

    assert retrieval.recipes == []
    assert retrieval.sources == []


def test_prompt_builder_uses_repository_context(tmp_path):
    template = tmp_path / "template.txt"
    template.write_text("Context:\n{recipes}\nQuestion:\n{question}", encoding="utf-8")
    recipe = retrieval_module.RetrievalRecipe("1", "Spicy Chicken", "Dinner", "spicy", ["chicken"], "Slice", "45", 0.91)

    prompt = prompt_module.PromptBuilder(template).build("What can I cook?", [recipe])

    assert "Recipe ID: 1" in prompt
    assert "Ingredients: chicken" in prompt
    assert "Similarity score: 0.910000" in prompt
    assert "Question:\nWhat can I cook?" in prompt


def test_assistant_uses_rag_for_repository_question_after_no_tool_call():
    result = SimpleNamespace(recipeId="1", title="Spicy Chicken", score=0.91)
    retrieval_service = retrieval_module.RetrievalService(StubSemanticSearchService([result]), StubMetadataProvider())
    prompt_builder = prompt_module.PromptBuilder()
    ollama = StubOllamaClient(SimpleNamespace(content="Direct draft", tool_calls=[]))
    service = assistant_module.AssistantService(
        ollama,
        StubConversationStore(),
        StubToolExecutor(),
        retrieval_service=retrieval_service,
        prompt_builder=prompt_builder,
    )

    response = service.chat("What spicy chicken meal can I cook?")

    assert "Use the spicy chicken recipe." in response.response
    assert "Sources:" in response.response
    assert "1: Spicy Chicken" in response.response
    assert len(ollama.calls) == 2
    assert ollama.calls[1]["tools"] == []


def test_assistant_preserves_tool_calling_before_rag():
    tool_executor = StubToolExecutor()
    ollama = StubOllamaClient(
        SimpleNamespace(content="", tool_calls=[{"name": "search_recipes", "arguments": {"query": "chicken"}}]),
        rag_content="final tool answer",
    )
    service = assistant_module.AssistantService(ollama, StubConversationStore(), tool_executor)

    response = service.chat("Search for chicken recipes")

    assert tool_executor.executed is True
    assert response.response == "final tool answer"
