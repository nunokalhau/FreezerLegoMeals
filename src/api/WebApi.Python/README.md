# Freezer Lego Meals Python API

This is the Python web API for Freezer Lego Meals project.

## Getting Started

### Prerequisites
- Python 3.8+

### Installation
1. Create a virtual environment:
```
python -m venv venv
```

2. Activate the virtual environment:
```
venv\Scripts\Activate.ps1
```

3. Install dependencies:
```
pip install -r requirements.txt
```

### Running the API
```
python app.py
```

The API will be available at `http://localhost:5000`

## Endpoints
- `GET /` - Root endpoint  
- `GET /health` - Health check endpoint
- `POST /api/recipes/search` - Search recipes by ingredients 
- `GET /api/recipes/{id}` - Get recipe by ID
- `POST /api/recipes/find-by-ingredients` - Find meals with specified ingredients
- `GET /api/recipes/{id}/details` - Get recipe details
- `GET /api/shopping/ingredients/{identifier}` - Get ingredients for a recipe
- `POST /api/shopping/ingredients` - Get ingredients for multiple recipes
- `POST /api/shopping/generate` - Generate shopping list from recipes
- `GET /api/shopping/{identifier}/info` - Get recipe info

## Features
- FastAPI framework with automatic OpenAPI/Swagger documentation
- Modular architecture
- Health check endpoint
- API documentation available at `/docs`

## Testing

### Swagger UI
The API documentation is available at:
- http://localhost:5000/docs (Interactive Swagger UI)
- http://localhost:5000/redoc (ReDoc documentation)

### Health Endpoint
```
GET /health
```
Returns:
```json
{
  "status": "healthy",
  "service": "WebApi.Python"
}
```

### Example Requests

#### Search Recipes by Ingredients
```
POST /api/recipes/search
Content-Type: application/json

{
  "ingredients": ["chicken", "rice", "broccoli"]
}
```

#### Get Recipe by ID
```
GET /api/recipes/123
```

#### Find Meals with Ingredients
```
POST /api/recipes/find-by-ingredients
Content-Type: application/json

{
  "query": "chicken and broccoli"
}
```

#### Get Recipe Details
```
GET /api/recipes/123/details
```

#### Get Ingredients for a Recipe
```
GET /api/shopping/ingredients/chicken-stir-fry
```

#### Generate Shopping List 
```
POST /api/shopping/generate
Content-Type: application/json

{
  "recipe_identifiers": ["chicken-stir-fry", "vegetable-curry"],
  "scale_factor": 1.5,
  "group_by_category": true
}
```

#### Get Recipe Info
```
GET /api/shopping/chicken-stir-fry/info
```

## Architecture
HTTP Request
→ Controller (FastAPI)
→ Service (MealService, ShoppingService)
→ Repository (via Services)
→ Data Source


## Notes
- API uses JSON for request and response bodies
- All endpoints are documented with schemas 
- CORS is enabled for all origins in development mode