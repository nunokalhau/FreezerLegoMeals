# Freezer Lego Meals NestJS Web API Unit Tests

This directory contains comprehensive Jest unit tests for the NestJS Web API component of the Freezer Lego Meals application.

## Test Structure

The test suite is organized into several logical categories:

### 1. Controller Tests
- `app.controller.spec.ts` - Tests for the main AppController endpoint functionality
- Validates controller structure and basic behavior

### 2. Integration Tests  
- `api.integration.test.ts` - Full API endpoint integration testing using supertest
- Tests HTTP request handling and response structure
- Validates proper routing and response handling

### 3. Module Tests
- `app.module.spec.ts` - Tests for application module configuration 
- Validates NestJS module compilation and structure

## Implementation Details

Based on the existing structure in `src\Api\FrezerLegoMeals.WebApi.NestJS`, this test suite includes:

1. **Controller Testing** - Verifies AppController functionality
2. **Service Integration** - Tests controller-service dependency injection  
3. **Endpoint Validation** - Tests root `/` endpoint behavior
4. **HTTP Request Simulation** - Uses supertest for real HTTP testing
5. **Module Compilation** - Validates NestJS module structure

## Test Framework

The tests use:
- **Jest** - Modern JavaScript testing framework with strong TypeScript support  
- **NestJS Testing Module** - For proper DI and application testing
- **supertest** - For HTTP request simulation and validation
- **TypeScript Compilation** - Full type checking and validation

## Usage

To run these tests:

```bash
# Using npm/yarn from project root or test directory
npm run test

# Or specifically for this test suite  
npm run test -- --testPathPattern="WebApi.NestJS.UnitTests"

# Run with coverage 
npm run test -- --coverage
```

## Current Coverage

The tests currently include:
- Main `/` endpoint verification 
- Controller and service integration testing
- Module compilation validation
- HTTP response structure testing
- Dependency injection pattern validation

## Future Expansion

As the API grows, additional controller tests can be added for:
- Meal service endpoints
- Shopping list endpoints
- Recipe search endpoints  
- Authentication/authorization endpoints
- Error handling scenarios

The test structure is designed to accommodate expansion while maintaining consistency with NestJS testing patterns and best practices.