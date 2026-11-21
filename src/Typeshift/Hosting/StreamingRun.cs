using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Typeshift;

// D64: a run's stream is the run, so reading it again would run the agent again: it is read once, typed or not
/// <summary>
/// A run that streams its updates and, once they are read to the end, holds its response. The model is called when
/// you read the updates, and the updates can be read once.
/// More in the guide:
/// <see href="https://weboost-at.github.io/Typeshift/guide/running-agents#streaming">Streaming</see>.
/// </summary>
public class StreamingRun : IAsyncEnumerable<ChatResponseUpdate>
{
    private readonly IAsyncEnumerable<ChatResponseUpdate> _updates;
    private ChatResponse? _response;
    private int _read;

    internal StreamingRun(IAsyncEnumerable<ChatResponseUpdate> updates) => _updates = updates;

    /// <summary>
    /// The run's response, made of its updates: its messages, the decisions it waits for and its conversation's id.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The updates are not read to their end: not yet, or never, because the run failed, was cancelled or was left
    /// early.
    /// </exception>
    public ChatResponse Response =>
        _response ?? throw new InvalidOperationException(
            "A streaming run has its response once its updates are read to the end.");

    /// <summary>
    /// Starts the run and streams its updates.
    /// </summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The updates, with the tool calls running between them.</returns>
    /// <exception cref="InvalidOperationException">
    /// The updates were read before. Their first <c>MoveNextAsync</c> throws it.
    /// </exception>
    public async IAsyncEnumerator<ChatResponseUpdate> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _read, 1) == 1)
            throw new InvalidOperationException("A streaming run streams its updates once; start a new run instead.");

        SynchronizationContexts.Leave();
        List<ChatResponseUpdate> updates = [];

        await foreach (var update in _updates.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            updates.Add(update);

            // D85: the caller asks for the next update, or leaves and so stops the run, from its own context
            try
            {
                yield return update;
            }
            finally
            {
                SynchronizationContexts.Leave();
            }
        }

        _response = updates.ToChatResponse();
    }
}

/// <summary>
/// A run that streams its updates and, once they are read to the end, holds its answer typed. The model is called
/// when you read the updates, and the updates can be read once.
/// </summary>
/// <typeparam name="TResult">The type the model answers in.</typeparam>
public sealed class StreamingRun<TResult> : StreamingRun
{
    private readonly JsonSerializerOptions _serializerOptions;
    private ChatResponse<TResult>? _response;

    internal StreamingRun(IAsyncEnumerable<ChatResponseUpdate> updates, JsonSerializerOptions serializerOptions)
        : base(updates) =>
        _serializerOptions = serializerOptions;

    /// <summary>
    /// The run's response, with the answer typed in <c>Result</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The updates are not read to their end: not yet, or never, because the run failed, was cancelled or was left
    /// early.
    /// </exception>
    public new ChatResponse<TResult> Response =>
        _response ??= new ChatResponse<TResult>(base.Response, _serializerOptions);
}
