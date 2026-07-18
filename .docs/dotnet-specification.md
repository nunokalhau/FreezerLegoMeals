# Freezer Lego Meals - .NET Solution Specification

## 1. Project Overview

### What is this application?
The Freezer Lego Meals application is a recipe management system that allows users to search, plan, and manage freezer meals. It provides functionality for finding recipes based on ingredients, generating shopping lists, and organizing meal combinations.

### What problem does it solve?
This application solves the problem of meal planning and preparation by providing:
- A centralized database of freezer-friendly recipes
- Smart ingredient-based recipe searching 
- Automated shopping list generation
- Meal combination management for convenient bulk preparation

### Who are the intended users?
- Home cooks who want to prepare meals in advance for freezing
- Busy individuals looking to save time on meal prep
- Families who plan and organize their weekly dinners
- Anyone interested in maintaining frozen meal collections

### Main use cases:
1. **Search recipes by ingredients** - Find recipes containing specific ingredients
2. **Get recipe details** - View complete recipe information including ingredients and preparation notes
3. **Generate shopping lists** - Create ingredient lists for multiple recipes with scaling options
4. **Manage meal combinations** - Organize recipes into predefined meal sets
5. **View recipe information** - Get quick overview of specific recipes

### Business goals:
- Provide an organized, searchable collection of freezer meals 
- Reduce time spent planning and shopping for frozen meals
- Enable easy meal preparation through automated process
- Support flexible meal planning with ingredient scaling capabilities

## 2. Business Domain

### Core Concepts:

#### Recipes
Recipes are the primary business entities in this application, containing:
- Identification information (ID, name, source path)
- Preparation details (tags, servings, time to prepare)  
- Cooking notes (prepping, freezing, reheating instructions)
- Combinations (related recipes that can be prepared together)

#### Ingredients
Ingredients are items used in recipes, with:
- Unique identifiers
- Descriptive names
- Relationships with recipes through RecipeIngredient entities

#### Shopping lists
Shopping lists are generated collections of ingredients needed for multiple recipes. Key aspects include:
- Scaling capabilities (e.g., doubling ingredient quantities)
- Organizing by categories
- Supporting bulk ingredient acquisition

#### Categories 
Recipe categories are expressed through tags and are used for searching and organization.

#### Measurements
Measurements are represented as a combination of amount (double) and unit (string).

#### Servings
The number of servings a recipe provides, helping with scaling.

#### Scaling recipes
Recipes can be scaled by a factor to adjust ingredient quantities. For example, a 2x scale would double all ingredients.

#### Recipe search
Multiple search capabilities:
- Search by specific ingredients 
- Natural language queries for finding meals with specified ingredients
- Finding ingredients within recipes

#### Recipe combinations
Recipes can be grouped into combinations (meal sets) which allows for organized planning and batch preparation. Examples include "Weekend Meal Combo" that combines different recipes.

### Concept Relationships:
Recipes contain ingredients through the RecipeIngredient relationship, forming a many-to-many connection where one recipe can have multiple ingredients and one ingredient can appear in multiple recipes.
RecipeCombinations organize recipes into meal sets, providing a way to plan combinations rather than individual meals.
Ingredients are linked to recipes through the RecipeIngredient entity that also stores amount and units of each ingredient.

## 3. Functional Requirements

### Feature: Search Recipes by Ingredients
#### Purpose:
Allow users to find recipes containing specific ingredients or ingredient types.

#### Inputs:
- Ingredient list (string array)

#### Outputs:
- List of matching recipes with their details

#### Business rules:
- Recipe ingredient matching is case-insensitive
- Only ingredients that appear in a recipe match are included  
- Search returns all matching recipes

#### Validation rules:
- Ingredients list cannot be null or empty
- Individual ingredient names should not be empty/whitespace

#### Error scenarios:
- Null ingredient list produces BadRequest with error message
- Invalid input results in HTTP 400 errors

### Feature: Get Recipe Details by ID  
#### Purpose:
Retrieve detailed information about a specific recipe.
 
#### Inputs:
- Recipe ID (integer)

#### Outputs:
- Recipe details including ingredients and preparation notes  

#### Business rules:
- Returns complete recipe information
- Handles cases where recipe is not found

#### Validation rules:
- Valid integer ID required

#### Error scenarios:
- Recipe ID not found results in 404 Not Found response

### Feature: Find Meals With Ingredients (Natural Language Query)
#### Purpose:
Enable users to search for meals with ingredients using natural language.
  
#### Inputs:
- Natural language query string

#### Outputs:
- List of matching recipes and search terms extracted from query
- Response message with search result information  

#### Business rules:
- Simple parsing extracts common food terms from queries
- Query parsing is basic - limited to predefined food terms

#### Validation rules:
- Query must be non-null and non-empty 

#### Error scenarios:
- Empty or null query results in BadRequest error

### Feature: Generate Shopping List
#### Purpose:
Create shopping lists for multiple recipes with scaling capabilities.
  
#### Inputs:
- List of recipe identifiers (names/IDs)
- Scale factor (double - default 1.0)
- Grouping by category (boolean - default true)

#### Outputs:
- Shopping list data including ingredients and metadata

#### Business rules:
- Ingredients are scaled by provided factor
- Ingredients can be grouped by category when desired
- Multiple recipes can be selected for shopping list generation

#### Validation rules:
- Recipe identifiers are required in the request body  
- Scale factor should be a valid positive number

#### Error scenarios:
- Missing recipe identifiers result in 400 errors
- Invalid scale factors should be handled gracefully  

### Feature: Get Recipe Ingredients 
#### Purpose:
Fetch complete ingredient lists for specific recipes.
  
#### Inputs:
- Recipe identifier (name or ID)

#### Outputs:
- List of ingredients with amounts and units 

#### Business rules:
- Supports both recipe names and IDs
- Returns associated ingredients with quantities

#### Validation rules:
- Identifier is required and must not be empty/whitespace

#### Error scenarios:
- Recipe not found returns 404

### Feature: Get Recipe Information  
#### Purpose:
Quick access to basic recipe information.
  
#### Inputs:
- Recipe identifier (name or ID)

#### Outputs:
- Basic recipe info including name, servings and prep time  

#### Business rules:
- Returns core recipe metadata  
- Handles different identifier types (name vs ID)

#### Validation rules:
- Identifier is required

#### Error scenarios:
- Recipe not found results in 404 errors

## 4. API Specification

### API Endpoint: Search Recipes by Ingredients
**Route:** POST /api/recipes/search
**HTTP Verb:** POST  
**Request Model:** SearchRecipesRequest (array of ingredient strings)
**Response Model:** SearchRecipesResponse (list of recipes + count)
**Status Codes:** 200 OK, 400 Bad Request (if no ingredients provided or null) 
**Validation:** Checks for non-null, non-empty ingredients list
**Business behaviour:** Finds all recipes containing any of the specified ingredients

### API Endpoint: Get Recipe by ID  
**Route:** GET /api/recipes/{id}  
**HTTP Verb:** GET
**Request Model:** GetRecipeByIdRequest (integer ID)
**Response Model:** GetRecipeByIdResponse (recipe details)
**Status Codes:** 200 OK, 400 Bad Request (invalid ID), 404 Not Found (missing recipe) 
**Validation:** Checks for valid integer ID greater than zero
**Business behaviour:** Retrieves detailed information for a specific recipe

### API Endpoint: Find Meals With Ingredients (Natural Query)
**Route:** POST /api/recipes/find-by-ingredients  
**HTTP Verb:** POST
**Request Model:** FindMealsWithIngredientsRequest (string query)
**Response Model:** FindMealsWithIngredientsResponse (recipes + search terms + message)
**Status Codes:** 200 OK, 400 Bad Request (if no query provided or null) 
**Validation:** Ensures query is not null or empty
**Business behaviour:** Parses natural language query and finds meals containing those ingredients

### API Endpoint: Get Recipe Details by ID
**Route:** GET /api/recipes/{id}/details  
**HTTP Verb:** GET
**Request Model:** GetRecipeByIdRequest (integer ID) 
**Response Model:** GetRecipeDetailsResponse (recipe + message)
**Status Codes:** 200 OK, 400 Bad Request (invalid ID), 404 Not Found (missing recipe)
**Validation:** Validates ID is proper integer greater than zero
**Business behaviour:** Returns detailed recipe information for a specific recipe

### API Endpoint: Get Recipe Ingredients  
**Route:** GET /api/shopping/ingredients/{identifier}  
**HTTP Verb:** GET
**Request Model:** GetRecipeRequest (string identifier) 
**Response Model:** GetRecipeIngredientsResponse (list of ingredients)
**Status Codes:** 200 OK, 400 Bad Request (missing identifier), 404 Not Found (no recipe found)
**Validation:** Checks that identifier is provided and non-empty
**Business behaviour:** Retrieves ingredient list for a single recipe

### API Endpoint: Get Multiple Recipe Ingredients  
**Route:** POST /api/shopping/ingredients  
**HTTP Verb:** POST 
**Request Model:** List of strings (recipe identifiers)
**Response Model:** GetMultipleRecipeIngredientsResponse (dictionaries mapping recipe names to ingredients)
**Status Codes:** 200 OK, 400 Bad Request (missing body or empty list)
**Validation:** Ensures request body is provided with at least one identifier
**Business behaviour:** Collects ingredient lists from multiple recipes in one call

### API Endpoint: Generate Shopping List  
**Route:** POST /api/shopping/generate  
**HTTP Verb:** POST
**Request Model:** GenerateShoppingListRequest (list of recipe identifiers, scale factor, group by category)
**Response Model:** GenerateShoppingListResponse (shopping list details + metadata) 
**Status Codes:** 200 OK, 400 Bad Request (missing body or identifiers)
**Validation:** Validates all required fields are present
**Business behaviour:** Creates a consolidated shopping list for multiple recipes with optional scaling

### API Endpoint: Get Recipe Info  
**Route:** GET /api/shopping/{identifier}/info  
**HTTP Verb:** GET
**Request Model:** GetRecipeRequest (string identifier)
**Response Model:** GetRecipeInfoResponse (basic recipe info)  
**Status Codes:** 200 OK, 400 Bad Request (missing identifier), 404 Not Found (no recipe found)
**Validation:** Checks that identifier is provided and non-empty
**Business behaviour:** Retrieves basic information about a specific recipe

## 5. Solution Architecture

### Overall solution structure:
The application follows a layered architecture pattern with clean separation of concerns:

- **WebApi.DotNet** - The API layer that exposes REST endpoints and handles HTTP requests/responses
- **Services.DotNet** - Business logic layer implementing domain services and use cases  
- **Repositories.DotNet** - Data access layer implementing repository pattern for database operations
- **Domain.DotNet** - Domain models representing business entities
- **Tests** - Unit/integration tests for the various layers

### Project structure:
1. **src/api/WebApi.DotNet/** - API layer with controllers, contracts, and endpoints
2. **src/services/Services.DotNet/** - Business logic services implementing IMealService and IShoppingService  
3. **src/repositories/Repository.DotNet/** - Data access layer using EF Core for database operations
4. **src/domain/Domain.DotNet/** - Domain models with entities and value objects
5. **tests/unit/** - Unit tests for each component 
6. **tests/integration/** - Integration tests for API behavior

### Layer responsibilities:
- **API Layer**: Handles HTTP requests, routing, validation, serialization and response formatting 
- **Service Layer**: Implements business rules, coordinates operations between repositories and domain models
- **Repository Layer**: Abstracts data access for database operations using EF Core
- **Domain Layer**: Contains core business entities and value objects without dependency on external frameworks

### Dependency Flow:
API → Services → Repository → Database (EF Core)
API ← Services ← Repository

### Dependency Injection:
Dependency injection is used throughout the application, configured in Program.cs:
- Controllers depend on IMealService and IShoppingService interfaces
- Services depend on IRecipeRepository interface
- Repositories depend on FreezerLegoMealsContext (EF Core context)

### Configuration:
Uses standard .NET configuration patterns with:
- appsettings.json for connection strings and other settings
- Dependency injection in Program.cs for service registration
- Swagger UI for API documentation

### Swagger:
API endpoints are documented using Swagger/OpenAPI specifications, making the APIs self-documenting and easily testable.

### Error handling:
Application uses standardized HTTP status codes:
- 200 OK for successful requests
- 400 Bad Request for validation errors
- 404 Not Found for missing resources
- Exception handling with appropriate status codes

### Logging:
Logging is configured through .NET's built-in logging system, with integration points available.

## 6. Internal Components

### Controllers
**RecipesController.cs**: Exposes endpoints related to recipes including searching by ingredients and fetching recipes by ID.
**ShoppingController.cs**: Exposes endpoints for shopping list generation including ingredient retrieval and shopping list creation.
**MealPlanningController.cs**: Placeholder for meal planning features.

### Services  
**MealService.cs**: Implements business logic for recipe search functionality, including searching by ingredients and getting recipe details. Uses dependency injection to get an IRecipeRepository instance.
**ShoppingService.cs**: Implements business logic for preparing shopping lists, including ingredient retrieval and list generation based on recipes.

### Repositories
**IRecipeRepository interface**: Defines methods contracts for recipe data access operations such as getting recipes, finding by ingredients, and retrieving combinations.
**RecipeRepository.cs**: Concrete implementation of IRecipeRepository that uses EF Core to map database entities to domain models. Includes mapping methods between database entities and domain models.

### DTOs (Data Transfer Objects)
**Request DTOs:** 
- FindMealsWithIngredientsRequest - natural language query
- GenerateShoppingListRequest - recipe list with scaling parameters  
- GetRecipeByIdRequest - recipe ID
- GetRecipeRequest - recipe identifier

**Response DTOs:**
- SearchRecipesResponse - list of recipes and count
- FindMealsWithIngredientsResponse - search results with message
- GetMultipleRecipeIngredientsResponse - dictionary of ingredients mapping
- GetRecipeIngredientsResponse - ingredient list  
- GenerateShoppingListResponse - shopping list results
- GetRecipeInfoResponse - basic recipe information
- GetRecipeDetailsResponse - detailed recipe information

### Domain models
**Recipe**: Represents a single meal recipe with all metadata and relationships to ingredients.
**Ingredient**: Represents an ingredient with name and connection to recipes.  
**RecipeIngredient**: Represents the many-to-many relationship between recipes and ingredients, storing quantity and unit information.
**RecipeCombination**: Groups related recipes together for organized meal planning.

### Interfaces
**IMealService**: Interface for recipe-related business logic. Defines methods for searching, getting by ID, finding with ingredients, and getting details.
**IShoppingService**: Interface for shopping list generation. Defines methods for retrieving ingredients, generating lists, and getting recipe info.
**IRecipeRepository**: Interface for data access operations related to recipes and ingredients.

## 7. Data Flow

### Request → Controller → Service → Repository → Database → Response

#### Search Recipes by Ingredients flow:
1. API endpoint receives POST /api/recipes/search with ingredient list
2. RecipesController calls MealService.SearchRecipesByIngredientsAsync()
3. MealService passes the ingredient list to IRecipeRepository.FindRecipesWithIngredientsAsync()  
4. RecipeRepository queries database using entity framework and EF Core LINQ queries
5. Database returns matching recipe entities 
6. RecipeRepository maps entities to domain models (Recipe objects)
7. Result returned to service, then to controller, then to client via JSON response

#### Generate Shopping List flow:
1. API endpoint receives POST /api/shopping/generate with recipe identifiers and optional parameters
2. ShoppingController calls ShoppingService.GenerateShoppingListAsync()
3. ShoppingService retrieves ingredients for each recipe by calling GetMultipleRecipeIngredientsAsync()
4. ShoppingService processes ingredients into consolidated shopping list format  
5. Result returned to controller, then to client via JSON response

#### Get Recipe Details flow:
1. API endpoint receives GET /api/recipes/{id}/details
2. RecipesController calls MealService.GetRecipeDetailsAsync()
3. MealService calls IRecipeRepository.GetRecipeByIdAsync() 
4. RecipeRepository queries database for recipe by ID and includes related ingredients
5. Database returns entity data  
6. RecipeRepository maps to domain model
7. Result returned through service to controller, then to client.

## 8. Testing Strategy

### Unit tests:
- Focus on individual components without external dependencies
- Mock repositories in service layer tests
- Test business logic with various scenarios, including edge cases
- Test validation behaviors
- Cover core functionality of each layer

### Integration tests:
- Test full integration between layers
- Validate database interactions 
- Test complete data flows through API endpoints
- Ensure proper behavior of controllers with real repository dependencies

### Test conventions:
- Tests follow .NET testing conventions using xUnit framework
- Each test is clearly named and describes what it's testing
- Use of fixtures and setup for repeatable testing scenarios  
- Tests are isolated - no shared state between tests

### Mocking strategy:
- Uses Moq or similar mocking libraries for dependencies
- Repository interfaces are mocked to isolate service logic from database
- Controllers use dependency injection for easy testing with mocked services 

### Test coverage:
- Aim for high code coverage of business logic
- Focus on critical paths and error handling scenarios
- Ensure all major functionality is tested through unit/integration tests

## 9. Design Principles

### Architectural patterns:
- **Repository Pattern**: Abstracts data access operations with clear interfaces.
- **Dependency Injection**: Services are injected via constructor parameters to promote loose coupling.
- **Layered Architecture**: Clear separation of concerns between API, Service, and Repository layers.

### SOLID principles:
- **Single Responsibility Principle**: Each class has one clear purpose (Controller for HTTP, Service for business logic, Repository for data access)
- **Open/Closed Principle**: System designed to be open for extension but closed for modification
- **Liskov Substitution**: Interfaces are properly implemented and can be substituted without affecting behavior 
- **Interface Segregation**: Services split into smaller, specific interfaces (IMealService, IShoppingService)
- **Dependency Inversion**: High-level components depend on abstractions, not concrete implementations

### Architecture Constraints:
- Controllers MUST NOT access repositories.
- Repositories MUST NOT contain business logic.
- Services MUST NOT access HTTP.
- DTOs MUST be immutable.
- Controllers only orchestrate.
- Repositories only persist.
- Services own the business logic.

### Separation of Concerns:
Each project layer has distinct responsibilities:
- WebApi: HTTP handling and API contracts
- Services: Business logic 
- Repositories: Data access logic  
- Domain: Core entities and business rules

### Repository pattern implementation:
- IRecipeRepository defines data access contract
- RecipeRepository implements data operations using EF Core
- Repository abstraction allows easy testing with mocks
- Clear separation between database schema and domain models

## 10. Database Schema

### Entity Relationships

The database uses a normalized structure with the following key relationships:

1. **Recipe ↔ Ingredient (Many-to-Many)**
   - Managed through the `RecipeIngredient` junction table
   - Each recipe ingredient record stores amount and unit information
   - Foreign keys: RecipeId → RecipeEntity, IngredientId → IngredientEntity

2. **Recipe ↔ RecipeCombination (One-to-Many)**
   - Through the `RecipeCombinationItem` junction table
   - Records define which recipes belong to which combinations with positioning
   - Foreign keys: CombinationId → RecipeCombinationEntity, RecipeId → RecipeEntity

3. **Ingredient ↔ Recipe (Many-to-Many)**
   - Direct relationship through the `RecipeIngredient` table
   
### Database Entities

#### RecipeEntity
Primary recipe table with:
- Id: Primary key, unique identifier
- Name: Required, string up to 255 characters
- SourcePath: Optional, path string up to 500 characters 
- Tags: Optional, string for categorization
- Servings: Optional integer
- TimeToPrepare: Optional integer (minutes)
- Prepping: Optional notes about prep work  
- FreezingNotes: Optional freezing instructions
- ReheatNotes: Optional reheating instructions
- Combinations: Optional text representing combination IDs
- Notes: Optional general recipe notes
- RecipeIngredients: Navigation property to ingredients in this recipe
- RecipeCombinationItems: Navigation property to combination items for this recipe

#### IngredientEntity  
Ingredient storage table with:
- Id: Primary key, unique identifier  
- Name: Required, string up to 255 characters
- RecipeIngredients: Navigation property to recipes that use this ingredient

#### RecipeCombinationEntity
Meal combinations table with:
- Id: Primary key, unique identifier
- Name: Required, string up to 255 characters
- Description: Optional description
- RecipeCombinationItems: Navigation property to items in this combination

#### RecipeIngredientEntity
Junction table for recipe-ingredient relationships with:
- Id: Primary key, unique identifier
- RecipeId: Foreign key to RecipeEntity
- IngredientId: Foreign key to IngredientEntity  
- Amount: Optional double (quantity)
- Unit: Optional string (unit of measure)
- Recipe: Navigation property to parent recipe
- Ingredient: Navigation property to ingredient

#### RecipeCombinationItemEntity
Junction table for combination-recipe relationships with:
- Id: Primary key, unique identifier
- CombinationId: Foreign key to RecipeCombinationEntity
- RecipeId: Foreign key to RecipeEntity
- Position: Integer positioning within the combination
- RecipeCombination: Navigation property to parent combination
- Recipe: Navigation property to recipe

## 11. Implementation Notes

### Assumptions:
1. Recipe data is already seeded in the database
2. Ingredients are properly mapped to recipes with accurate quantities and units
3. Database connection strings are configured correctly
4. Entity Framework Core handles relationships and mapping automatically 

### Limitations:
1. Natural language query parsing for search is basic, only detecting a predefined set of food terms
2. Shopping list generation lacks full categorization functionality (the feature exists in interface but isn't fully implemented)
3. There's limited error handling beyond basic validation 

### Unfinished areas:
1. The grouping functionality in GenerateShoppingListAsync is specified but not fully implemented in the service layer
2. More sophisticated recipe combination logic could be added
3. Search result details (like relevance scoring) are not implemented

### Migration considerations:
When implementing this in another language, the primary challenges involve:
1. Database layer: EF Core's mapping and entity framework features need to be replaced with equivalent persistence mechanisms 
2. Dependency injection: The .NET dependency injection container needs replacement or adaptation
3. API design: REST endpoints and DTO structures would need to be reimplemented using the target language's preferred web framework
4. Entity mapping: The manual mapping between database entities and domain models would require similar approach in other languages

### Key implementation details:
1. The system uses an in-memory database for unit tests but connects to SQL Server in production via dependency injection
2. All HTTP responses are serialized to JSON automatically through .NET's built-in serialization with proper content-negotiation
3. Database relationships are handled via EF Core's navigation properties and foreign key constraints  
4. The service layer handles business logic rules without direct database access, promoting maintainability and testability

