using Microsoft.EntityFrameworkCore;
using Repository.DotNet.Entities;

namespace Repository.DotNet;

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
        modelBuilder.Entity<IngredientEntity>(entity =>
        {
            entity.ToTable("ingredients");
            entity.HasKey(candidate => candidate.Id);

            entity.Property(candidate => candidate.Id).HasColumnName("id");
            entity.Property(candidate => candidate.Name).HasColumnName("name").IsRequired();
            entity.Property(candidate => candidate.Unit).HasColumnName("unit");

            entity.HasIndex(candidate => candidate.Name).IsUnique();
        });

        modelBuilder.Entity<RecipeEntity>(entity =>
        {
            entity.ToTable("recipes");
            entity.HasKey(candidate => candidate.Id);

            entity.Property(candidate => candidate.Id).HasColumnName("id");
            entity.Property(candidate => candidate.Name).HasColumnName("name").IsRequired();
            entity.Property(candidate => candidate.SourcePath).HasColumnName("source_path");
            entity.Property(candidate => candidate.Tags).HasColumnName("tags");
            entity.Property(candidate => candidate.Servings).HasColumnName("servings");
            entity.Property(candidate => candidate.TimeToPrepare).HasColumnName("time_to_prepare");
            entity.Property(candidate => candidate.Prepping).HasColumnName("prepping");
            entity.Property(candidate => candidate.FreezingNotes).HasColumnName("freezing_notes");
            entity.Property(candidate => candidate.ReheatNotes).HasColumnName("reheat_notes");
            entity.Property(candidate => candidate.Combinations).HasColumnName("combinations");
            entity.Property(candidate => candidate.Notes).HasColumnName("notes");

            entity.HasIndex(candidate => candidate.Name).IsUnique();
        });

        modelBuilder.Entity<RecipeCombinationEntity>(entity =>
        {
            entity.ToTable("recipe_combinations");
            entity.HasKey(candidate => candidate.Id);

            entity.Property(candidate => candidate.Id).HasColumnName("id");
            entity.Property(candidate => candidate.Name).HasColumnName("name").IsRequired();
            entity.Property(candidate => candidate.Description).HasColumnName("description");
            entity.Property(candidate => candidate.Notes).HasColumnName("notes");

            entity.HasIndex(candidate => candidate.Name).IsUnique();
        });

        modelBuilder.Entity<RecipeIngredientEntity>()
            .ToTable("recipe_ingredients");

        modelBuilder.Entity<RecipeIngredientEntity>()
            .HasKey(candidate => new { candidate.RecipeId, candidate.IngredientId });

        modelBuilder.Entity<RecipeIngredientEntity>()
            .Property(candidate => candidate.RecipeId)
            .HasColumnName("recipe_id");

        modelBuilder.Entity<RecipeIngredientEntity>()
            .Property(candidate => candidate.IngredientId)
            .HasColumnName("ingredient_id");

        modelBuilder.Entity<RecipeIngredientEntity>()
            .Property(candidate => candidate.Amount)
            .HasColumnName("amount");

        modelBuilder.Entity<RecipeIngredientEntity>()
            .Property(candidate => candidate.AmountText)
            .HasColumnName("amount_text");

        modelBuilder.Entity<RecipeIngredientEntity>()
            .Property(candidate => candidate.Unit)
            .HasColumnName("unit");

        modelBuilder.Entity<RecipeIngredientEntity>()
            .Property(candidate => candidate.Notes)
            .HasColumnName("notes");

        modelBuilder.Entity<RecipeIngredientEntity>()
            .Property(candidate => candidate.SourceText)
            .HasColumnName("source_text");

        modelBuilder.Entity<RecipeIngredientEntity>()
            .HasIndex(candidate => candidate.IngredientId)
            .HasDatabaseName("idx_recipe_ingredients_ingredient_id");

        modelBuilder.Entity<RecipeIngredientEntity>()
            .HasOne(ri => ri.Recipe)
            .WithMany(r => r.RecipeIngredients)
            .HasForeignKey(ri => ri.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RecipeIngredientEntity>()
            .HasOne(ri => ri.Ingredient)
            .WithMany(i => i.RecipeIngredients)
            .HasForeignKey(ri => ri.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RecipeCombinationItemEntity>()
            .ToTable("recipe_combination_items");

        modelBuilder.Entity<RecipeCombinationItemEntity>()
            .HasKey(candidate => new { candidate.CombinationId, candidate.RecipeId });

        modelBuilder.Entity<RecipeCombinationItemEntity>()
            .Property(candidate => candidate.CombinationId)
            .HasColumnName("combination_id");

        modelBuilder.Entity<RecipeCombinationItemEntity>()
            .Property(candidate => candidate.RecipeId)
            .HasColumnName("recipe_id");

        modelBuilder.Entity<RecipeCombinationItemEntity>()
            .Property(candidate => candidate.Position)
            .HasColumnName("position");

        modelBuilder.Entity<RecipeCombinationItemEntity>()
            .Property(candidate => candidate.Notes)
            .HasColumnName("notes");

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