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
- `POST /recipes/search` - Search recipes by ingredients 
- `POST /shopping-list/generate` - Generate shopping list from recipes

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
  "service": "FreezerLegoMeals.WebApi.Python"
}
```

### Example Requests

#### Search Recipes by Ingredients
```
POST /recipes/search
Content-Type: application/json

{
  "ingredients": ["chicken", "rice", "broccoli"]
}
```

#### Generate Shopping List 
```
POST /shopping-list/generate
Content-Type: application/json

{
  "recipe_names": ["Chicken Stir Fry", "Vegetable Curry"],
  "scale_factor": 1.5
}
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