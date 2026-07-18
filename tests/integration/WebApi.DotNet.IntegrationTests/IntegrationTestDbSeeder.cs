using Repository.DotNet;
using Repository.DotNet.Entities;

namespace WebApi.DotNet.IntegrationTests
{
    /// <summary>
    /// Seeded test data for integration tests.
    /// </summary>
    public static class IntegrationTestDbSeeder
    {
        /// <summary>
        /// Seeds the database with test data.
        /// </summary>
        /// <param name="context">The database context to seed.</param>
        public static void SeedTestData(FreezerLegoMealsContext context)
        {
            // Clear existing data
            context.RecipeCombinations.RemoveRange(context.RecipeCombinations);
            context.RecipeIngredients.RemoveRange(context.RecipeIngredients);
            context.RecipeCombinationItems.RemoveRange(context.RecipeCombinationItems);
            context.Recipes.RemoveRange(context.Recipes);
            context.Ingredients.RemoveRange(context.Ingredients);

            // Add test ingredients
            var ingredient1 = new IngredientEntity { Id = 1, Name = "Chicken" };
            var ingredient2 = new IngredientEntity { Id = 2, Name = "Rice" };
            var ingredient3 = new IngredientEntity { Id = 3, Name = "Broccoli" };
            var ingredient4 = new IngredientEntity { Id = 4, Name = "Beef" };
            var ingredient5 = new IngredientEntity { Id = 5, Name = "Tomato" };
            var ingredient6 = new IngredientEntity { Id = 6, Name = "Garlic" };

            context.Ingredients.AddRange(ingredient1, ingredient2, ingredient3, ingredient4, ingredient5, ingredient6);

            // Add test combinations
            var combination1 = new RecipeCombinationEntity 
            { 
                Id = 1, 
                Name = "Main Meal", 
                Description = "Primary meal combination" 
            };

            var combination2 = new RecipeCombinationEntity 
            { 
                Id = 2, 
                Name = "Side Dish", 
                Description = "Side dish combination" 
            };

            context.RecipeCombinations.AddRange(combination1, combination2);

            // Add test recipes
            var recipe1 = new RecipeEntity
            {
                Id = 1,
                Name = "Chicken Fried Rice",
                SourcePath = "/recipes/chicken_fried_rice.md",
                Tags = "chicken,rice,fast",
                Servings = 4,
                TimeToPrepare = 30,
                Prepping = "Chop ingredients",
                FreezingNotes = "Freezes well for up to 3 months",
                ReheatNotes = "Reheat with steam",
                Combinations = "1",
                Notes = "Delicious chicken fried rice recipe"
            };

            var recipe2 = new RecipeEntity
            {
                Id = 2,
                Name = "Beef Stir Fry",
                SourcePath = "/recipes/beef_stir_fry.md",
                Tags = "beef,vegetables,quick",
                Servings = 3,
                TimeToPrepare = 25,
                Prepping = "Cut vegetables",
                FreezingNotes = "Freezes well for up to 2 months",
                ReheatNotes = "Reheat in microwave",
                Combinations = "1,2",
                Notes = "Quick beef stir fry recipe"
            };

            var recipe3 = new RecipeEntity
            {
                Id = 3,
                Name = "Broccoli Beef",
                SourcePath = "/recipes/broccoli_beef.md",
                Tags = "beef,vegetables,healthy",
                Servings = 2,
                TimeToPrepare = 35,
                Prepping = "Clean vegetables",
                FreezingNotes = "Freezes well for up to 4 months",
                ReheatNotes = "Reheat on stove",
                Combinations = "2",
                Notes = "Healthy broccoli beef recipe"
            };

            var recipe4 = new RecipeEntity
            {
                Id = 4,
                Name = "Tomato Garlic Pasta",
                SourcePath = "/recipes/tomato_garlic_pasta.md",
                Tags = "pasta,vegetables,quick",
                Servings = 2,
                TimeToPrepare = 20,
                Prepping = "Chop tomatoes and garlic",
                FreezingNotes = "Freezes well for up to 1 month",
                ReheatNotes = "Reheat on stove or microwave",
                Combinations = "1",
                Notes = "Quick tomato garlic pasta recipe"
            };

            context.Recipes.AddRange(recipe1, recipe2, recipe3, recipe4);

            // Add recipe ingredient relationships
            var recipeIngredient1 = new RecipeIngredientEntity 
            { 
                RecipeId = 1, 
                IngredientId = 1,
                Amount = 1.0,
                Unit = "lb"
            };

            var recipeIngredient2 = new RecipeIngredientEntity 
            { 
                RecipeId = 1, 
                IngredientId = 2,
                Amount = 2.0,
                Unit = "cups"
            };

            var recipeIngredient3 = new RecipeIngredientEntity 
            { 
                RecipeId = 2, 
                IngredientId = 4,
                Amount = 1.5,
                Unit = "lb"
            };

            var recipeIngredient4 = new RecipeIngredientEntity 
            { 
                RecipeId = 2, 
                IngredientId = 3,
                Amount = 1.0,
                Unit = "head"
            };

            var recipeIngredient5 = new RecipeIngredientEntity 
            { 
                RecipeId = 3, 
                IngredientId = 4,
                Amount = 1.0,
                Unit = "lb"
            };

            var recipeIngredient6 = new RecipeIngredientEntity 
            { 
                RecipeId = 3, 
                IngredientId = 3,
                Amount = 2.0,
                Unit = "cups"
            };

            var recipeIngredient7 = new RecipeIngredientEntity 
            { 
                RecipeId = 4, 
                IngredientId = 5,
                Amount = 3.0,
                Unit = "tomatoes"
            };

            var recipeIngredient8 = new RecipeIngredientEntity 
            { 
                RecipeId = 4, 
                IngredientId = 6,
                Amount = 4.0,
                Unit = "cloves"
            };

            context.RecipeIngredients.AddRange(recipeIngredient1, recipeIngredient2, 
                                               recipeIngredient3, recipeIngredient4, 
                                               recipeIngredient5, recipeIngredient6,
                                               recipeIngredient7, recipeIngredient8);

            // Add recipe combination items
            var combinationItem1 = new RecipeCombinationItemEntity
            {
                Id = 1,
                CombinationId = 1,
                RecipeId = 1,
                Position = 1
            };

            var combinationItem2 = new RecipeCombinationItemEntity
            {
                Id = 2,
                CombinationId = 1,
                RecipeId = 2,
                Position = 2
            };

            var combinationItem3 = new RecipeCombinationItemEntity
            {
                Id = 3,
                CombinationId = 2,
                RecipeId = 3,
                Position = 1
            };

            var combinationItem4 = new RecipeCombinationItemEntity
            {
                Id = 4,
                CombinationId = 1,
                RecipeId = 4,
                Position = 3
            };

            context.RecipeCombinationItems.AddRange(combinationItem1, combinationItem2, combinationItem3, combinationItem4);

            // Save changes to database
            context.SaveChanges();
        }
    }
}