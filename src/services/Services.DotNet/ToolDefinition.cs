using System.Text.Json.Serialization;

namespace Services.DotNet;

public sealed class ToolDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("script")]
    public string Script { get; init; } = string.Empty;

    [JsonPropertyName("keywords")]
    public IReadOnlyList<string> Keywords { get; init; } = [];

    [JsonPropertyName("parameters")]
    public IReadOnlyList<string> Parameters { get; init; } = [];

    [JsonPropertyName("examples")]
    public IReadOnlyList<string> Examples { get; init; } = [];

    [JsonPropertyName("output_description")]
    public string OutputDescription { get; init; } = string.Empty;

    [JsonPropertyName("aliases")]
    public IReadOnlyList<string> Aliases { get; init; } = [];
}