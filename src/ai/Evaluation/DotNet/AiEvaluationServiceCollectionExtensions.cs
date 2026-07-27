using Microsoft.Extensions.DependencyInjection;

namespace Evaluation.DotNet;

public static class AiEvaluationServiceCollectionExtensions
{
    public static IServiceCollection AddAiEvaluationFramework(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IAiEvaluationTraceContext, AiEvaluationTraceContext>();
        services.AddSingleton<IAiEvaluationScenarioCatalog, DefaultAiEvaluationScenarioCatalog>();
        services.AddSingleton<IAiEvaluationDimension, RoutingDecisionEvaluationDimension>();
        services.AddSingleton<IAiEvaluationDimension, RetrievalRelevanceEvaluationDimension>();
        services.AddSingleton<IAiEvaluationDimension, SemanticSearchQualityEvaluationDimension>();
        services.AddSingleton<IAiEvaluationDimension, ToolSelectionEvaluationDimension>();
        services.AddSingleton<IAiEvaluationDimension, ToolExecutionEvaluationDimension>();
        services.AddSingleton<IAiEvaluationDimension, MemoryRetrievalEvaluationDimension>();
        services.AddSingleton<IAiEvaluationDimension, GroundedResponseEvaluationDimension>();
        services.AddSingleton<IAiEvaluationDimension, OverallAnswerQualityEvaluationDimension>();
        services.AddScoped<IAiEvaluationService, AiEvaluationService>();

        return services;
    }
}
