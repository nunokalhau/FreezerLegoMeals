# Freezer Lego Meals .NET Web API Unit Tests

This directory contains comprehensive xUnit v3 unit tests for the .NET Web API component of the Freezer Lego Meals application.

## Test Structure

The test suite is organized into several logical categories:

### 1. Health Controller Tests
- `HealthControllerTests.cs` - Tests for the core health endpoint functionality
- Validates that `/api/health` returns correct responses and structure

### 2. API Integration Tests  
- `ApiControllerTests.cs` - General API controller integration and configuration tests
- Tests startup and dependency injection patterns

### 3. Route and Endpoint Tests
- `ApiRouteTests.cs` - Tests for endpoint routing and path consistency
- Validates RESTful conventions are followed

### 4. Global and Infrastructure Tests
- `GlobalApiTest.cs` - Overall API structure and functionality validation
- Tests test infrastructure and project setup

## Implementation Details

Based on the existing structure in `src\Api\FreezerLegoMeals.WebApi.DotNet`, this test suite includes:

1. **Health Endpoint Testing** - Testing the `/api/health` endpoint that returns system status
2. **API Configuration Validation** - Ensures proper ASP.NET Core startup and dependency registration  
3. **RESTful Pattern Compliance** - Validates API follows standard .NET Web API conventions
4. **Test Infrastructure Setup** - Uses `Microsoft.AspNetCore.Mvc.Testing` for integration testing

## Test Framework

The tests use:
- **xUnit v3** - Modern testing framework with strong .NET integration
- **Microsoft.AspNetCore.Mvc.Testing** - For integration testing with actual HTTP request handling  
- **Moq** - For mocking dependencies where needed
- **Newtonsoft.Json** - For JSON response parsing and validation

## Usage

To run these tests:

```bash
# Using .NET CLI from the test project directory
dotnet test

# Using Visual Studio Test Explorer
# (Tests should appear automatically)

# Using VS Code test runner  
# (If configured)
```

## Current Coverage

The tests currently include:
- Health endpoint response validation
- Basic API startup and configuration testing
- Route pattern verification 
- Project structure validation

## Future Expansion

As the API grows, additional controller tests can be added for:
- Meal service endpoints
- Shopping list endpoints  
- Recipe search endpoints
- Combination management endpoints
- Authentication/authorization endpoints

The test structure is designed to accommodate expansion while maintaining consistency with existing .NET testing patterns.