using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Typeshift;

// D36: every event Typeshift logs itself, under one category and with an id and a name that stay, so a filter or an
// alert names the event and never its text. A new event takes the next number, and a number is never used again
internal static partial class LogEvents
{
    public const string Category = "Typeshift";

    // The app's logger, or none: a runtime of TypeshiftRuntime.Create has one once its chain registered logging (D75)
    public static ILogger For(IServiceProvider services) => For(services.GetService<ILoggerFactory>());

    public static ILogger For(ILoggerFactory? loggers) => loggers?.CreateLogger(Category) ?? NullLogger.Instance;

    [LoggerMessage(
        1,
        LogLevel.Warning,
        "No store is registered, so Typeshift keeps settings, conversations and decisions in memory and loses them "
        + "when the app stops: call WithStore<T>() after AddTypeshift() to keep them, or WithFileStore() to keep "
        + "agents' own settings in files.")]
    public static partial void NoStore(ILogger logger);

    [LoggerMessage(2, LogLevel.Error, "Conversation {Conversation} could not store a step.")]
    public static partial void StepNotStored(ILogger logger, Exception exception, StoreId? conversation);

    [LoggerMessage(3, LogLevel.Error, "Conversation {Conversation} could not close its decisions.")]
    public static partial void DecisionsNotClosed(ILogger logger, Exception exception, StoreId? conversation);

    [LoggerMessage(
        4,
        LogLevel.Warning,
        "Conversation {Conversation} is stored without the {Content} contents of its steps: "
        + "Microsoft.Extensions.AI's content contract cannot write that type.")]
    public static partial void ContentNotStored(ILogger logger, StoreId? conversation, string? content);

    [LoggerMessage(
        5,
        LogLevel.Warning,
        "Conversation {Conversation} holds {Count} contents this version of Microsoft.Extensions.AI cannot read, and "
        + "the run goes on without them.")]
    public static partial void ContentNotRead(ILogger logger, StoreId conversation, int count);

    [LoggerMessage(6, LogLevel.Error, "The background agent {Agent} failed in {Conversation}.")]
    public static partial void BackgroundAgentFailed(
        ILogger logger,
        Exception exception,
        string agent,
        StoreId? conversation);

    [LoggerMessage(
        7,
        LogLevel.Warning,
        "Agent {Agent} could not summarize its conversation with model {Model} of provider {Provider}: {Next}.")]
    public static partial void SummaryFailed(
        ILogger logger,
        Exception exception,
        string agent,
        string? model,
        ProviderId? provider,
        string next);

    [LoggerMessage(
        8,
        LogLevel.Error,
        "The call {CallId} of {Tool} reached the tool loop without a verdict of the gate, so it did not run.")]
    public static partial void CallWithoutVerdict(ILogger logger, string callId, string tool);

    [LoggerMessage(
        9,
        LogLevel.Warning,
        "A watcher of {Watched} lost its oldest updates, since more than {Limit} waited for it: read the feed "
        + "faster, or raise MaxPendingUpdates with WithRealtimeTracking.")]
    public static partial void UpdatesDropped(ILogger logger, string watched, int limit);

    [LoggerMessage(
        10,
        LogLevel.Debug,
        "Agent {Agent} runs on model {Model} of provider {Provider}, served by {Client}, with the settings of "
        + "{Layers}.")]
    public static partial void RunResolved(
        ILogger logger,
        string agent,
        string? model,
        ProviderId? provider,
        string client,
        string layers);

    [LoggerMessage(
        11,
        LogLevel.Debug,
        "Agent {Agent} compacted a call of {Messages} messages from about {Tokens} tokens to {CompactedMessages} "
        + "messages of about {CompactedTokens}.")]
    public static partial void Compacted(
        ILogger logger,
        string agent,
        int messages,
        int tokens,
        int compactedMessages,
        int compactedTokens);

    [LoggerMessage(12, LogLevel.Debug, "The approval rule {Rule} {Verdict} the call {CallId} of {Tool}.")]
    public static partial void RuleDecided(ILogger logger, string rule, string verdict, string callId, string tool);

    [LoggerMessage(13, LogLevel.Debug, "Waiting for {LockFile}, which another save holds.")]
    public static partial void FileStoreWaiting(ILogger logger, string lockFile);

    [LoggerMessage(14, LogLevel.Information, "The file store keeps agents' own settings in {Folder}.")]
    public static partial void FileStoreFolder(ILogger logger, string folder);
}
