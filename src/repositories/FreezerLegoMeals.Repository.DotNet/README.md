# Freezer Lego Meals .NET Repository Layer

This directory contains the data access repository implementation for the .NET service layer.

## Overview

The repository layer provides an abstraction over data sources, implementing clean architecture patterns that separate data access logic from business logic in the .NET service layer.

## Implementation Details

### Interfaces

- **`IRecipeRepository`** - Defines contract for recipe data operations:
  - Get all recipes
  - Get recipe by ID 
  - Find recipes with ingredients
  - Retrieve combinations and ingredients

### Classes

- **`RecipeRepository`** - Concrete implementation of `IRecipeRepository`
  - Implements database operations with async patterns
  - Connection string handling for data sources  
  - Proper error handling and validation

## Design Patterns

1. **Repository Pattern** - Abstracts data access for clean separation
2. **Async/Await** - Non-blocking operations for scalability  
3. **Dependency Injection** - Integration with .NET DI container
4. **Interface-based Programming** - For testability and mocking

## Usage Examples

```csharp
// In service layer
var repository = new RecipeRepository("connection-string");
var recipes = await repository.GetRecipesAsync();

// Query by ingredients 
var matchingRecipes = await repository.FindRecipesWithIngredientsAsync(
    new[] { "chicken", "onion" }
);
```

## Integration

This repository integrates with:
- `FreezerLegoMeals.Services.DotNet` service layer
- ASP.NET Core dependency injection system
- Data access configuration via connection strings