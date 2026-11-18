using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D31: below the run middleware. The agent's context providers add instructions after the agent's, messages ahead
// of the input, which the conversation never stores, and tools for this run only. A provider the run's services
// do not hold is created for the pass and disposed once the pass ended, since the tools it returned run until then
internal sealed class ContextChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (options is not ResolvedChatOptions { AgentRun.Agent.ContextProviders.Count: > 0 } resolved)
            return await base.GetResponseAsync(messages, options, cancellationToken);

        List<IContextProvider> created = [];

        try
        {
            (messages, options) = await AddContextAsync(messages, resolved, created, cancellationToken);
            return await base.GetResponseAsync(messages, options, cancellationToken);
        }
        finally
        {
            await DisposeAsync(created);
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<IContextProvider> created = [];

        try
        {
            if (options is ResolvedChatOptions { AgentRun.Agent.ContextProviders.Count: > 0 } resolved)
                (messages, options) = await AddContextAsync(messages, resolved, created, cancellationToken);

            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                yield return update;
            }
        }
        finally
        {
            await DisposeAsync(created);
        }
    }

    // D31: the provider the run's services hold, which their scope disposes, or one created from them for the pass
    public static ContextSource Of<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(
        IServiceProvider services)
        where TProvider : class, IContextProvider =>
        services.GetService<TProvider>() is { } registered
            ? new(registered, Created: false)
            : new(ActivatorUtilities.CreateInstance<TProvider>(services), Created: true);

    private static async Task<(List<ChatMessage>, ChatOptions)> AddContextAsync(
        IEnumerable<ChatMessage> messages,
        ResolvedChatOptions resolved,
        List<IContextProvider> created,
        CancellationToken ct)
    {
        var run = resolved.AgentRun!;
        var call = messages.ToList();
        var options = (ResolvedChatOptions)resolved.Clone();
        var at = Math.Min(run.InputStart, call.Count);

        foreach (var source in run.Agent.ContextProviders)
        {
            var (provider, isCreated) = source(run.Services);
            if (isCreated) created.Add(provider);

            var context = await provider.GetContextAsync(run, ct);

            if (context.Instructions is { } instructions)
            {
                options.Instructions = options.Instructions is { } own ? $"{own}\n\n{instructions}" : instructions;
            }

            call.InsertRange(at, context.Messages);
            at += context.Messages.Count;

            options.Tools = [.. options.Tools ?? [], .. context.Tools];
            resolved.ConversationRun?.MarkTransient(context.Messages, added: true);
        }

        return (call, options);
    }

    // As a scope disposes what it created: asynchronously when the provider can, and a failure is the pass's
    private static async Task DisposeAsync(List<IContextProvider> created)
    {
        foreach (var provider in created)
        {
            if (provider is IAsyncDisposable disposable) await disposable.DisposeAsync();
            else (provider as IDisposable)?.Dispose();
        }
    }
}

// D31: a context provider for one pass, and whether the pass created it, which then disposes it
internal readonly record struct ContextSource(IContextProvider Provider, bool Created);
