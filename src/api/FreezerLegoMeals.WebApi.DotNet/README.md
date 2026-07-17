# Freezer Lego Meals C# API

This is the C# web API for Freezer Lego Meals project built with ASP.NET Core.

## Getting Started

### Prerequisites
- .NET 8.0 SDK

### Running the API
```
dotnet run
```

The API will be available at `http://localhost:5001`

## Endpoints
- `GET /api/health` - Health check endpoint

## Features
- ASP.NET Core web API framework
- Swagger documentation
- Health check endpoint

## Testing

### Swagger UI
The API documentation is available at:
- http://localhost:5001/swagger (Interactive Swagger UI)

### Health Endpoint
```
GET /api/health
```
Returns:
```json
{
  "status": "healthy",
  "service": "FreezerLegoMeals.WebApi.DotNet"
}
```

## Architecture
HTTP Request
→ Controller (ASP.NET Core)
→ Service (MealService, ShoppingService) 
→ Repository (RecipeRepository)
→ Data Source

## Notes
- API uses JSON for request and response bodies
- Swagger documentation is automatically generated
- All endpoints are documented with schemas