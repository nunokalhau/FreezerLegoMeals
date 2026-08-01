namespace RAG.DotNet;

public sealed class RecipeStartupIndexingOptions
{
    public bool Enabled { get; set; }

    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromMinutes(5);

    public string ProbeQuery { get; set; } = "freezer meal prep recipes";

    public int ProbeTopK { get; set; } = 1;
}
