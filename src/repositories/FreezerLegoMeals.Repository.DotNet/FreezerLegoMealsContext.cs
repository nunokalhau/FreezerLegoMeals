using Microsoft.EntityFrameworkCore;
using FreezerLegoMeals.Repository.DotNet.Entities;

namespace FreezerLegoMeals.Repository.DotNet;

/// <summary>
/// Database context for the Freezer Lego Meals application.
/// </summary>
public class FreezerLegoMealsContext : DbContext
{
    public FreezerLegoMealsContext(DbContextOptions<FreezerLegoMealsContext> options) : base(options)
    {
    }

    public DbSet<RecipeEntity> Recipes { get; set; }
    public DbSet<IngredientEntity> Ingredients { get; set; }
    public DbSet<RecipeCombinationEntity> RecipeCombinations { get; set; }
    public DbSet<RecipeIngredientEntity> RecipeIngredients { get; set; }
    public DbSet<RecipeCombinationItemEntity> RecipeCombinationItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure RecipeIngredientEntity relationships
        modelBuilder.Entity<RecipeIngredientEntity>()
            .HasOne(ri => ri.Recipe)
            .WithMany(r => r.RecipeIngredients)
            .HasForeignKey(ri => ri.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RecipeIngredientEntity>()
            .HasOne(ri => ri.Ingredient)
            .WithMany(i => i.RecipeIngredients)
            .HasForeignKey(ri => ri.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure RecipeCombinationItemEntity relationships
        modelBuilder.Entity<RecipeCombinationItemEntity>()
            .HasOne(rci => rci.RecipeCombination)
            .WithMany(rc => rc.RecipeCombinationItems)
            .HasForeignKey(rci => rci.CombinationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RecipeCombinationItemEntity>()
            .HasOne(rci => rci.Recipe)
            .WithMany(r => r.RecipeCombinationItems)
            .HasForeignKey(rci => rci.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        base.OnModelCreating(modelBuilder);
    }
}