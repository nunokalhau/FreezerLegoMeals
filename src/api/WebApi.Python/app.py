from fastapi import FastAPI, HTTPException, Body
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import Dict, Any, List, Optional
import json
import sys
from pathlib import Path

# Load service modules from absolute paths so startup is independent of shell cwd.
import importlib.util

SRC_ROOT = Path(__file__).resolve().parents[2]
ASSISTANT_SERVICE_PATH = SRC_ROOT / "services" / "Services.Python" / "assistant_service.py"
MEAL_SERVICE_PATH = SRC_ROOT / "services" / "Services.Python" / "meal_service.py"
OLLAMA_CLIENT_PATH = SRC_ROOT / "services" / "Services.Python" / "ollama_client.py"
EMBEDDING_SERVICE_PATH = SRC_ROOT / "ai" / "Embedding.Python" / "embedding_service.py"
VECTOR_STORE_PATH = SRC_ROOT / "ai" / "VectorStores" / "Python" / "local_vector_store.py"
SEMANTIC_SEARCH_PATH = SRC_ROOT / "ai" / "SemanticSearch" / "Python" / "semantic_search_service.py"
RAG_RETRIEVAL_PATH = SRC_ROOT / "ai" / "RAG" / "Python" / "retrieval_service.py"
RAG_PROMPT_BUILDER_PATH = SRC_ROOT / "ai" / "RAG" / "Python" / "prompt_builder.py"
CONVERSATION_STORE_PATH = SRC_ROOT / "services" / "Services.Python" / "conversation_store.py"
SHOPPING_SERVICE_PATH = SRC_ROOT / "services" / "Services.Python" / "shopping_service.py"
TOOL_EXECUTOR_PATH = SRC_ROOT / "services" / "Services.Python" / "tool_executor.py"
TOOL_REGISTRY_PATH = SRC_ROOT / "tools" / "tool_registry.json"
REPOSITORY_PATH = SRC_ROOT / "repositories" / "Repository.Python" / "__init__.py"
EMBEDDINGS_DIR = SRC_ROOT.parent / "data" / "embeddings"

assistant_spec = importlib.util.spec_from_file_location("services_python_assistant", ASSISTANT_SERVICE_PATH)
meal_spec = importlib.util.spec_from_file_location("services_python_meal", MEAL_SERVICE_PATH)
ollama_spec = importlib.util.spec_from_file_location("services_python_ollama", OLLAMA_CLIENT_PATH)
embedding_spec = importlib.util.spec_from_file_location("embedding_python_service", EMBEDDING_SERVICE_PATH)
vector_store_spec = importlib.util.spec_from_file_location("vector_store_python", VECTOR_STORE_PATH)
semantic_search_spec = importlib.util.spec_from_file_location("semantic_search_python", SEMANTIC_SEARCH_PATH)
rag_retrieval_spec = importlib.util.spec_from_file_location("rag_python_retrieval", RAG_RETRIEVAL_PATH)
rag_prompt_builder_spec = importlib.util.spec_from_file_location("rag_python_prompt_builder", RAG_PROMPT_BUILDER_PATH)
conversation_store_spec = importlib.util.spec_from_file_location("services_python_conversation_store", CONVERSATION_STORE_PATH)
shopping_spec = importlib.util.spec_from_file_location("services_python_shopping", SHOPPING_SERVICE_PATH)
tool_executor_spec = importlib.util.spec_from_file_location("services_python_tool_executor", TOOL_EXECUTOR_PATH)
repository_spec = importlib.util.spec_from_file_location("repository_python", REPOSITORY_PATH)

if assistant_spec is None or assistant_spec.loader is None:
    raise ImportError(f"Unable to load AssistantService module from {ASSISTANT_SERVICE_PATH}")
if meal_spec is None or meal_spec.loader is None:
    raise ImportError(f"Unable to load MealService module from {MEAL_SERVICE_PATH}")
if ollama_spec is None or ollama_spec.loader is None:
    raise ImportError(f"Unable to load OllamaClient module from {OLLAMA_CLIENT_PATH}")
if embedding_spec is None or embedding_spec.loader is None:
    raise ImportError(f"Unable to load EmbeddingService module from {EMBEDDING_SERVICE_PATH}")
if vector_store_spec is None or vector_store_spec.loader is None:
    raise ImportError(f"Unable to load VectorStore module from {VECTOR_STORE_PATH}")
if semantic_search_spec is None or semantic_search_spec.loader is None:
    raise ImportError(f"Unable to load SemanticSearch module from {SEMANTIC_SEARCH_PATH}")
if rag_retrieval_spec is None or rag_retrieval_spec.loader is None:
    raise ImportError(f"Unable to load RAG Retrieval module from {RAG_RETRIEVAL_PATH}")
if rag_prompt_builder_spec is None or rag_prompt_builder_spec.loader is None:
    raise ImportError(f"Unable to load RAG PromptBuilder module from {RAG_PROMPT_BUILDER_PATH}")
if conversation_store_spec is None or conversation_store_spec.loader is None:
    raise ImportError(f"Unable to load ConversationStore module from {CONVERSATION_STORE_PATH}")
if shopping_spec is None or shopping_spec.loader is None:
    raise ImportError(f"Unable to load ShoppingService module from {SHOPPING_SERVICE_PATH}")
if tool_executor_spec is None or tool_executor_spec.loader is None:
    raise ImportError(f"Unable to load ToolExecutor module from {TOOL_EXECUTOR_PATH}")
if repository_spec is None or repository_spec.loader is None:
    raise ImportError(f"Unable to load Repository module from {REPOSITORY_PATH}")

assistant_module = importlib.util.module_from_spec(assistant_spec)
sys.modules[assistant_spec.name] = assistant_module
assistant_spec.loader.exec_module(assistant_module)
meal_module = importlib.util.module_from_spec(meal_spec)
sys.modules[meal_spec.name] = meal_module
meal_spec.loader.exec_module(meal_module)
ollama_module = importlib.util.module_from_spec(ollama_spec)
sys.modules[ollama_spec.name] = ollama_module
ollama_spec.loader.exec_module(ollama_module)
embedding_module = importlib.util.module_from_spec(embedding_spec)
sys.modules[embedding_spec.name] = embedding_module
embedding_spec.loader.exec_module(embedding_module)
vector_store_module = importlib.util.module_from_spec(vector_store_spec)
sys.modules[vector_store_spec.name] = vector_store_module
vector_store_spec.loader.exec_module(vector_store_module)
semantic_search_module = importlib.util.module_from_spec(semantic_search_spec)
sys.modules[semantic_search_spec.name] = semantic_search_module
semantic_search_spec.loader.exec_module(semantic_search_module)
rag_retrieval_module = importlib.util.module_from_spec(rag_retrieval_spec)
sys.modules[rag_retrieval_spec.name] = rag_retrieval_module
rag_retrieval_spec.loader.exec_module(rag_retrieval_module)
rag_prompt_builder_module = importlib.util.module_from_spec(rag_prompt_builder_spec)
sys.modules[rag_prompt_builder_spec.name] = rag_prompt_builder_module
rag_prompt_builder_spec.loader.exec_module(rag_prompt_builder_module)
conversation_store_module = importlib.util.module_from_spec(conversation_store_spec)
sys.modules[conversation_store_spec.name] = conversation_store_module
conversation_store_spec.loader.exec_module(conversation_store_module)
shopping_module = importlib.util.module_from_spec(shopping_spec)
sys.modules[shopping_spec.name] = shopping_module
shopping_spec.loader.exec_module(shopping_module)
tool_executor_module = importlib.util.module_from_spec(tool_executor_spec)
sys.modules[tool_executor_spec.name] = tool_executor_module
tool_executor_spec.loader.exec_module(tool_executor_module)
repository_module = importlib.util.module_from_spec(repository_spec)
sys.modules[repository_spec.name] = repository_module
repository_spec.loader.exec_module(repository_module)

AssistantService = assistant_module.AssistantService
MealService = meal_module.MealService
OllamaClient = ollama_module.OllamaClient
OllamaEmbeddingService = embedding_module.OllamaEmbeddingService
LocalVectorStore = vector_store_module.LocalVectorStore
SemanticSearchService = semantic_search_module.SemanticSearchService
RecipeMetadataProvider = semantic_search_module.RecipeMetadataProvider
RetrievalService = rag_retrieval_module.RetrievalService
PromptBuilder = rag_prompt_builder_module.PromptBuilder
InMemoryConversationStore = conversation_store_module.InMemoryConversationStore
ShoppingService = shopping_module.ShoppingService
ToolRegistry = tool_executor_module.ToolRegistry
ToolExecutor = tool_executor_module.ToolExecutor
Repository = repository_module.Repository

app = FastAPI(
    title="Freezer Lego Meals Python API",
    description="API for Freezer Lego Meals project with modular meal prep capabilities.",
    version="1.0.0",
    redoc_url=None
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Initialize the services as singletons
ollama_client = OllamaClient()
embedding_service = OllamaEmbeddingService()
recipe_repository = Repository()
semantic_metadata_provider = RecipeMetadataProvider(recipe_repository)
semantic_search_service = SemanticSearchService(
    embedding_service,
    LocalVectorStore(EMBEDDINGS_DIR),
    semantic_metadata_provider,
)
retrieval_service = RetrievalService(semantic_search_service, semantic_metadata_provider)
prompt_builder = PromptBuilder()
conversation_store = InMemoryConversationStore()
tool_registry = ToolRegistry(TOOL_REGISTRY_PATH)
tool_executor = ToolExecutor(tool_registry)
assistant_service = AssistantService(ollama_client, conversation_store, tool_executor, retrieval_service=retrieval_service, prompt_builder=prompt_builder)
meal_service = MealService()
shopping_service = ShoppingService()

class HealthResponse(BaseModel):
    status: str
    service: str

class RecipeSearchRequest(BaseModel):
    ingredients: list[str]

class SearchRecipesResponse(BaseModel):
    recipes: List[Dict[str, Any]]
    total_recipes_found: int

class GetRecipeByIdRequest(BaseModel):
    id: int

class GetRecipeByIdResponse(BaseModel):
    recipe: Optional[Dict[str, Any]]

class FindMealsWithIngredientsRequest(BaseModel):
    query: str

class FindMealsWithIngredientsResponse(BaseModel):
    query: str
    search_terms: List[str]
    total_recipes_found: int
    recipes: List[Dict[str, Any]]
    message: str

class GetRecipeDetailsResponse(BaseModel):
    recipe: Optional[Dict[str, Any]]
    message: str

class GetRecipeIngredientsRequest(BaseModel):
    identifier: str

class GetRecipeIngredientsResponse(BaseModel):
    ingredients: List[Dict[str, Any]]
    recipe_name: str
    found: bool

class GetMultipleRecipeIngredientsRequest(BaseModel):
    recipe_identifiers: List[str]

class GetMultipleRecipeIngredientsResponse(BaseModel):
    recipe_ingredients: Dict[str, List[Dict[str, Any]]]
    total_recipes: int
    found: bool

class GenerateShoppingListRequest(BaseModel):
    recipe_identifiers: List[str]
    scale_factor: float = 1.0
    group_by_category: bool = True

class GenerateShoppingListResponse(BaseModel):
    shopping_list: Dict[str, Any]
    message: str
    scale_factor: float
    group_by_category: bool

class GetRecipeInfoRequest(BaseModel):
    identifier: str

class GetRecipeInfoResponse(BaseModel):
    info: Optional[Dict[str, Any]]

class AssistantChatRequest(BaseModel):
    message: str
    conversationId: Optional[str] = None

class AssistantChatResponse(BaseModel):
    conversationId: str
    response: str

class EmbeddingRequest(BaseModel):
    text: str

class EmbeddingResponse(BaseModel):
    model: str
    dimensions: int
    embedding: List[float]

class SemanticSearchRequest(BaseModel):
    query: str
    topK: int = 5

class SemanticSearchResponse(BaseModel):
    recipeId: str
    title: str
    score: float
    matchedText: str
    reason: str

@app.get("/")
def read_root():
    return {
        "message": "Welcome to Freezer Lego Meals Python API",
        "status": "success"
    }

@app.get("/health", response_model=HealthResponse)
def health_check():
    return HealthResponse(
        status="healthy",
        service="WebApi.Python"
    )

@app.post("/api/recipes/search")
def search_recipes(request: RecipeSearchRequest):
    """Search for recipes by ingredients."""
    if not request.ingredients or len(request.ingredients) == 0:
        raise HTTPException(status_code=400, detail="Ingredients list cannot be empty")
    
    # Delegate to the service
    recipes = meal_service.search_recipes_by_ingredients(request.ingredients)
    return SearchRecipesResponse(
        recipes=recipes,
        total_recipes_found=len(recipes)
    )

@app.get("/api/recipes/{id}", response_model=GetRecipeByIdResponse)
def get_recipe_by_id(id: int):
    """Get recipe by ID."""
    # Validate ID parameter exists and is positive integer
    if id <= 0:
        raise HTTPException(status_code=400, detail="ID must be a positive integer")
    
    try:
        recipe = meal_service.get_recipe_by_id(id)
        if not recipe:
            raise HTTPException(status_code=404, detail="Recipe not found")
        return GetRecipeByIdResponse(recipe=recipe)
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error retrieving recipe: {str(e)}")

@app.post("/api/recipes/find-by-ingredients")
def find_meals_with_ingredients(request: FindMealsWithIngredientsRequest):
    """Find meals containing specified ingredients (natural language query)."""
    # Validate request body is not null (Pydantic handles this via BaseModel)
    # Validate query string is not null/empty
    if not request.query or len(request.query.strip()) == 0:
        raise HTTPException(status_code=400, detail="Query string cannot be null or empty")
    
    try:
        result = meal_service.find_meals_with_ingredients(request.query)
        return FindMealsWithIngredientsResponse(
            query=result["query"],
            search_terms=result["search_terms"],
            total_recipes_found=result["total_recipes_found"],
            recipes=result["recipes"],
            message=result["message"]
        )
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error finding meals: {str(e)}")

@app.post("/api/assistant/chat", response_model=AssistantChatResponse)
def chat_with_assistant(request: AssistantChatRequest):
    """Send a basic chat message to the assistant."""
    if not request.message or len(request.message.strip()) == 0:
        raise HTTPException(status_code=400, detail="Message is required")

    try:
        response = assistant_service.chat(request.message, request.conversationId)
        return AssistantChatResponse(conversationId=response.conversation_id, response=response.response)
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error calling Ollama: {str(e)}")

@app.post("/embeddings", response_model=EmbeddingResponse)
@app.post("/api/embeddings", response_model=EmbeddingResponse)
def generate_embedding(request: EmbeddingRequest):
    """Generate an embedding vector for supplied text."""
    if not request.text or len(request.text.strip()) == 0:
        raise HTTPException(status_code=400, detail="Text is required")

    try:
        result = embedding_service.generate_embedding(request.text)
        return EmbeddingResponse(model=result.model, dimensions=result.dimensions, embedding=result.embedding)
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error generating embedding: {str(e)}")

@app.post("/api/semantic-search", response_model=List[SemanticSearchResponse])
def semantic_search(request: SemanticSearchRequest):
    """Search recipes semantically using the prebuilt local embedding index."""
    if not request.query or len(request.query.strip()) == 0:
        raise HTTPException(status_code=400, detail="Query is required")

    try:
        return [SemanticSearchResponse(**result.__dict__) for result in semantic_search_service.search(request.query, request.topK)]
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error running semantic search: {str(e)}")

@app.get("/api/recipes/{id}/details", response_model=GetRecipeDetailsResponse)
def get_recipe_details(id: int):
    """Get detailed recipe information."""
    if id <= 0:
        raise HTTPException(status_code=400, detail="ID must be a positive integer")
    
    try:
        result = meal_service.get_recipe_details(id)
        if "error" in result:
            raise HTTPException(status_code=404, detail=result["error"])
        return GetRecipeDetailsResponse(
            recipe=result.get("recipe"),
            message=result.get("message", "")
        )
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error retrieving recipe details: {str(e)}")

@app.get("/api/shopping/ingredients/{identifier}", response_model=GetRecipeIngredientsResponse)
def get_recipe_ingredients(identifier: str):
    """Get ingredients for a specific recipe."""
    # Validate identifier parameter exists and is not empty
    if not identifier or len(identifier.strip()) == 0:
        raise HTTPException(status_code=400, detail="Identifier cannot be null or empty")
    
    try:
        ingredients = shopping_service.get_recipe_ingredients(identifier)
        return GetRecipeIngredientsResponse(
            ingredients=ingredients,
            recipe_name=identifier,
            found=len(ingredients) > 0
        )
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error retrieving ingredients: {str(e)}")

@app.post("/api/shopping/ingredients", response_model=GetMultipleRecipeIngredientsResponse)
def get_multiple_recipe_ingredients(request: Any = Body(...)):
    """Get ingredients for multiple recipes."""
    # Validate request body is not null (Pydantic handles this via BaseModel)
    # Validate recipe identifiers list is not empty
    recipe_identifiers: List[str]
    if isinstance(request, list):
        recipe_identifiers = request
    elif isinstance(request, dict):
        recipe_identifiers = request.get("recipe_identifiers") or request.get("recipeIdentifiers") or []
    else:
        recipe_identifiers = []

    if not recipe_identifiers or len(recipe_identifiers) == 0:
        raise HTTPException(status_code=400, detail="Recipe identifiers list cannot be empty")
    
    try:
        recipe_ingredients = shopping_service.get_multiple_recipe_ingredients(recipe_identifiers)
        return GetMultipleRecipeIngredientsResponse(
            recipe_ingredients=recipe_ingredients,
            total_recipes=len(recipe_ingredients),
            found=len(recipe_ingredients) > 0
        )
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error retrieving multiple ingredients: {str(e)}")

@app.post("/api/shopping/generate", response_model=GenerateShoppingListResponse)
def generate_shopping_list(request: GenerateShoppingListRequest):
    """Generate a shopping list from recipes."""
    if not request.recipe_identifiers or len(request.recipe_identifiers) == 0:
        raise HTTPException(status_code=400, detail="Recipe identifiers list cannot be empty")
    
    if request.scale_factor is not None and request.scale_factor <= 0:
        raise HTTPException(status_code=400, detail="Scale factor must be greater than 0")
    
    try:
        result = shopping_service.generate_shopping_list(
            request.recipe_identifiers,
            request.scale_factor,
            request.group_by_category
        )
        return GenerateShoppingListResponse(
            shopping_list=result,
            message=result.get("message", ""),
            scale_factor=request.scale_factor,
            group_by_category=request.group_by_category
        )
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error generating shopping list: {str(e)}")

@app.get("/api/shopping/{identifier}/info", response_model=GetRecipeInfoResponse)
def get_recipe_info(identifier: str):
    """Get basic information about a recipe."""
    # Validate identifier parameter exists and is not empty
    if not identifier or len(identifier.strip()) == 0:
        raise HTTPException(status_code=400, detail="Identifier cannot be null or empty")
    
    try:
        info = shopping_service.get_recipe_info(identifier)
        if not info:
            raise HTTPException(status_code=404, detail="Recipe info not found")
        return GetRecipeInfoResponse(info=info)
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error retrieving recipe info: {str(e)}")

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=3000)