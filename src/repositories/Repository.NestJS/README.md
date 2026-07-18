# Freezer Lego Meals NestJS Repository Layer

This directory contains the data access repository implementation for the NestJS API layer.

## Overview

The repository layer provides an abstraction over data sources, implementing clean architecture patterns that separate data access logic from business logic in the NestJS API layer.

## Implementation Details

### Interfaces

- **`RecipeRepositoryInterface`** - Defines contract for recipe data operations:
  - Get all recipes
  - Get recipe by ID 
  - Find recipes with ingredients
  - Retrieve combinations and ingredients

### Classes

- **`RecipeRepository`** - Concrete implementation of `RecipeRepositoryInterface`
  - Implements async data access patterns for Node.js
  - Integrated with NestJS dependency injection system  
  - Follows TypeScript/JavaScript best practices

## Design Patterns

1. **Repository Pattern** - Abstracts data access for clean separation
2. **Async/Await** - Non-blocking operations for Node.js environment
3. **Dependency Injection** - Integration with NestJS DI container
4. **Interface-based Programming** - For testability and mocking

## Usage Examples

```typescript
// In service layer  
const repository = new RecipeRepository();
const recipes = await repository.getRecipes();

// Query by ingredients
const matchingRecipes = await repository.findRecipesWithIngredients([
    'chicken', 
    'onion'
]);
```

## Integration

This repository integrates with:
- `Services.NestJS` service layer  
- NestJS dependency injection system
- HTTP request handling in API controllers

## Development Notes

The repository is designed to be extended for specific data sources:
- In-memory implementations for testing
- Database implementations for production
- External API integrations when needed