namespace Services.DotNet;

public interface IAssistantLanguageDetector
{
    string? Detect(string message);
}