namespace Repository.DotNet.Entities;

public class RecipeIndexMetadataEntity
{
    public int RecipeId { get; set; }

    public string LanguageCoverage { get; set; } = string.Empty;

    public string ProjectionFingerprint { get; set; } = string.Empty;

    public string ProjectionSchemaVersion { get; set; } = string.Empty;

    public DateTime ProjectionGeneratedAtUtc { get; set; }

    public RecipeEntity Recipe { get; set; } = null!;
}
