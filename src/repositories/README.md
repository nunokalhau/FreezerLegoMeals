# Freezer Lego Meals Repository Layer

This directory contains repository implementations for data access in the Freezer Lego Meals application.

## Repository Structure

### .NET Repository
- `FreezerLegoMeals.Repository.DotNet` - Data access layer for .NET services
  - Implements database operations and data retrieval patterns
  - Follows clean architecture with clear separation of concerns

### NestJS Repository  
- `FreezerLegoMeals.Repository.NestJS` - Data access layer for NestJS APIs
  - Provides data access patterns for Node.js environment
  - Integrates with NestJS dependency injection system

## Implementation Details

Both repository implementations follow standard patterns:

1. **Interface-based Design** - Clear contracts for repository operations
2. **Async Operations** - Non-blocking data access patterns
3. **Error Handling** - Proper exception management
4. **Dependency Injection** - Integration with corresponding service frameworks

## .NET Repository Features

- `IRecipeRepository` interface defines all required data operations  
- `RecipeRepository` implementation provides concrete data access logic
- Base repository class for shared functionality
- Connection string handling and validation
- Async method signatures for proper performance

## NestJS Repository Features

- `RecipeRepositoryInterface` defines repository contract
- `RecipeRepository` implements data access patterns 
- Base repository with common utility methods
- Integration with NestJS decorators and dependency injection
- Async/await support for Node.js environment

## Usage

Repositories are intended to be used by service layers:

```csharp
// .NET usage
var repository = new RecipeRepository(connectionString);
var recipes = await repository.GetRecipesAsync();

// NestJS usage  
const repository = new RecipeRepository();
const recipes = await repository.getRecipes();
```

The repository pattern ensures clean separation between data access logic and business logic, supporting testability and maintainability.