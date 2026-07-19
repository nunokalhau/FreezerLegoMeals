using System.Text.Json;

namespace Services.DotNet;

public sealed record AssistantToolCall(string Name, IReadOnlyDictionary<string, object?> Arguments)
{
    public static AssistantToolCall FromJsonArguments(string name, JsonElement arguments)
    {
        var parameters = JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments.GetRawText())
            ?? new Dictionary<string, object?>();

        return new AssistantToolCall(name, parameters);
    }
}