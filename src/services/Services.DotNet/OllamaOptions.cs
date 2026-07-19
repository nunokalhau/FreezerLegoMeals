namespace Services.DotNet;

public class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";

    public string DefaultModel { get; set; } = "llama3.2";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}