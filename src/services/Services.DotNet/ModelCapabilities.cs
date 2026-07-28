namespace Services.DotNet;

public sealed record ModelCapabilities(
    string Model,
    bool? SupportsToolCalling = null,
    bool? SupportsVision = null,
    bool? SupportsStructuredOutput = null,
    bool? SupportsStreaming = null,
    bool? SupportsEmbeddings = null,
    int? MaxContextWindow = null)
{
    public static ModelCapabilities Unknown(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        return new ModelCapabilities(model.Trim());
    }
}
