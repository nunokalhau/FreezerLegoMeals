from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import Dict, Any
import json

# Import our architecture layers correctly
from src.services.FreezerLegoMeals.Services.Python import MealService, ShoppingService

app = FastAPI(title="Freezer Lego Meals Python API")

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

class ShoppingListRequest(BaseModel):
    recipe_names: list[str]
    scale_factor: float = 1.0

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
        service="FreezerLegoMeals.WebApi.Python"
    )

@app.post("/recipes/search")
def search_recipes(request: RecipeSearchRequest):
    """Search for recipes by ingredients."""
    # Delegate to the service
    result = meal_service.search_recipes_by_ingredients(" ".join(request.ingredients))
    return result

@app.post("/shopping-list/generate")
def generate_shopping_list(request: ShoppingListRequest):
    """Generate a shopping list from recipe names."""
    # Delegate to the service
    result = shopping_service.generate_shopping_list(
        request.recipe_names,
        scale_factor=request.scale_factor
    )
    return result

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=5000)