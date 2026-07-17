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
  - Integrates with EF Core for data access
  - Proper error handling and validation
  - Maps from EF Core entities to domain models

- **`FreezerLegoMealsContext`** - Database context for EF Core
  - Configuration of DbContext with DbSet properties
  - Entity relationship configuration using Fluent API

- **Entity Classes**
  - `RecipeEntity` - Recipe database entity
  - `IngredientEntity` - Ingredient database entity  
  - `RecipeCombinationEntity` - Recipe combination database entity
  - `RecipeIngredientEntity` - Join entity for recipe-ingredient relations
  - `RecipeCombinationItemEntity` - Join entity for combination-recipe relations

## Design Patterns

1. **Repository Pattern** - Abstracts data access for clean separation
2. **Async/Await** - Non-blocking operations for scalability  
3. **Dependency Injection** - Integration with .NET DI container
4. **Interface-based Programming** - For testability and mocking
5. **Mapping Pattern** - Transforms EF Core entities to domain models

## Usage Examples

```csharp
// In service layer
var repository = new RecipeRepository(context);
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
- EF Core for data persistence and mapping

## Key Features

- Full implementation of all public repository methods
- Correct mapping between database entities and domain models 
- Preserves the exact same public interface as specified
- No database entities leak outside the repository layer
- Uses proper navigation properties throughout mapping process
- Implements complete CRUD patterns with async operations