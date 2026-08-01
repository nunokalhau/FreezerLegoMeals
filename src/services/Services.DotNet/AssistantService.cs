using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Domain.DotNet;
using Orchestration.DotNet;

namespace Services.DotNet;

public class AssistantService : IAssistantService
{
    private static readonly ActivitySource ActivitySource = new("FreezerLegoMeals.AI");
    private readonly IConversationStore _conversationStore;
    private readonly IAssistantOrchestrator _orchestrator;
    private readonly ILanguageContextResolver _languageContextResolver;
    private readonly ILocalizationOptionsFactory _localizationOptionsFactory;
    private readonly IAssistantLanguageDetector _languageDetector;
    private readonly ILogger<AssistantService> _logger;
    private readonly AssistantOptions _options;
    private readonly AssistantLocalizationDefaultsOptions _localizationDefaults;

    public AssistantService(
        IConversationStore conversationStore,
        IAssistantOrchestrator orchestrator,
        ILanguageContextResolver languageContextResolver,
        ILocalizationOptionsFactory localizationOptionsFactory,
        IOptions<AssistantOptions> options,
        IOptions<AssistantLocalizationDefaultsOptions> localizationDefaults,
        ILogger<AssistantService> logger,
        IAssistantLanguageDetector? languageDetector = null)
    {
        _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _languageContextResolver = languageContextResolver ?? throw new ArgumentNullException(nameof(languageContextResolver));
        _localizationOptionsFactory = localizationOptionsFactory ?? throw new ArgumentNullException(nameof(localizationOptionsFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _localizationDefaults = localizationDefaults?.Value ?? throw new ArgumentNullException(nameof(localizationDefaults));
        _languageDetector = languageDetector ?? new HeuristicAssistantLanguageDetector(Microsoft.Extensions.Options.Options.Create(_localizationDefaults));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AssistantChatResult> ChatAsync(
        string message,
        string? conversationId = null,
        AssistantLocalizationRequest? localization = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required", nameof(message));

        using var activity = ActivitySource.StartActivity("assistant.chat", ActivityKind.Server);
        activity?.SetTag("assistant.conversation_id.input", conversationId ?? string.Empty);
        activity?.SetTag("assistant.user_message_length", message.Length);

        var conversation = _conversationStore.GetOrCreateConversation(conversationId);
        activity?.SetTag("assistant.conversation_id", conversation.ConversationId);
        var now = DateTimeOffset.UtcNow;
        var currentUserMessage = new ConversationMessage(ConversationRole.User, message, now);
        var messages = new List<ConversationMessage>
        {
            new(ConversationRole.System, _options.SystemPrompt, now)
        };
        messages.AddRange(conversation.Messages);
        messages.Add(currentUserMessage);
        var messagesToPersist = new List<ConversationMessage> { currentUserMessage };

        var detectedLanguage = string.IsNullOrWhiteSpace(localization?.ExplicitLanguage)
            ? _languageDetector.Detect(message)
            : null;

        var languageContext = _languageContextResolver.Resolve(
            explicitLanguage: localization?.ExplicitLanguage,
            detectedLanguage: detectedLanguage,
            negotiatedLanguages: localization?.NegotiatedLanguages,
            defaultLanguage: ResolveDefaultLanguage(),
            strictMode: localization?.StrictMode ?? false);
        var localizationOptions = _localizationOptionsFactory.Create(languageContext);

        var context = new OrchestratorContext(
            message,
            now,
            Guid.NewGuid().ToString("N"),
            new Dictionary<string, object?>(),
            languageContext,
            localizationOptions,
            conversation.ConversationId,
            messages,
            messagesToPersist,
            _options);
        var result = await _orchestrator.ExecuteAsync(context, cancellationToken);
        activity?.SetTag("assistant.selected_agent", result.SelectedAgent);
        activity?.SetTag("assistant.execution_duration_ms", result.ExecutionDuration.TotalMilliseconds);
        activity?.SetTag("assistant.error_count", result.Errors.Count);
        activity?.SetTag("assistant.executed_tool_count", result.ExecutedTools.Count);
        activity?.SetTag("assistant.retrieved_recipe_count", result.RetrievedRecipes.Count);

        _conversationStore.AppendMessages(conversation.ConversationId, result.MessagesToPersist);
        if (result.Errors.Count > 0)
            _logger.LogWarning("Assistant request completed with orchestration errors: {AssistantErrors}", string.Join("; ", result.Errors));

        _logger.LogInformation(
            "Assistant chat completed conversation={ConversationId} selectedAgent={SelectedAgent} durationMs={DurationMs} toolCalls={ToolCalls} retrievedRecipes={RetrievedRecipes} errors={ErrorCount}",
            conversation.ConversationId,
            result.SelectedAgent,
            result.ExecutionDuration.TotalMilliseconds,
            result.ExecutedTools.Count,
            result.RetrievedRecipes.Count,
            result.Errors.Count);

        return new AssistantChatResult(conversation.ConversationId, result.FinalResponse);
    }

    private string ResolveDefaultLanguage()
    {
        if (string.IsNullOrWhiteSpace(_localizationDefaults.DefaultLanguage))
        {
            return "en";
        }

        return _localizationDefaults.DefaultLanguage.Trim();
    }
}