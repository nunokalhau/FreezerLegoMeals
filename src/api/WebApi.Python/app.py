from fastapi import FastAPI, HTTPException, Body
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import Dict, Any, List, Optional
import json
from pathlib import Path

# Load service modules from absolute paths so startup is independent of shell cwd.
import importlib.util

SRC_ROOT = Path(__file__).resolve().parents[2]
MEAL_SERVICE_PATH = SRC_ROOT / "services" / "Services.Python" / "meal_service.py"
SHOPPING_SERVICE_PATH = SRC_ROOT / "services" / "Services.Python" / "shopping_service.py"

meal_spec = importlib.util.spec_from_file_location("services_python_meal", MEAL_SERVICE_PATH)
shopping_spec = importlib.util.spec_from_file_location("services_python_shopping", SHOPPING_SERVICE_PATH)

if meal_spec is None or meal_spec.loader is None:
    raise ImportError(f"Unable to load MealService module from {MEAL_SERVICE_PATH}")
if shopping_spec is None or shopping_spec.loader is None:
    raise ImportError(f"Unable to load ShoppingService module from {SHOPPING_SERVICE_PATH}")

meal_module = importlib.util.module_from_spec(meal_spec)
meal_spec.loader.exec_module(meal_module)
shopping_module = importlib.util.module_from_spec(shopping_spec)
shopping_spec.loader.exec_module(shopping_module)

MealService = meal_module.MealService
ShoppingService = shopping_module.ShoppingService

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