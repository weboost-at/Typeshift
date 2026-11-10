using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// A run that streams its updates and, once they are read to the end, holds its answer typed. The model is called
/// when you read the updates, and the updates can be read once.
/// </summary>
/// <typeparam name="TResult">The type the model answers in.</typeparam>
public sealed class StreamingRun<TResult> : IAsyncEnumerable<ChatResponseUpdate>
{
    private readonly IAsyncEnumerable<ChatResponseUpdate> _updates;
    private readonly JsonSerializerOptions _serializerOptions;
    private ChatResponse<TResult>? _response;
    private int _read;

    internal StreamingRun(IAsyncEnumerable<ChatResponseUpdate> updates, JsonSerializerOptions serializerOptions)
    {
        _updates = updates;
        _serializerOptions = serializerOptions;
    }

    /// <summary>
    /// The run's response, with the answer typed in <c>Result</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The updates are not read to their end: not yet, or never, because the run failed, was cancelled or was left
    /// early.
    /// </exception>
    public ChatResponse<TResult> Response =>
        _response ?? throw new InvalidOperationException(
            "A streaming run has its response once its updates are read to the end.");

    /// <summary>
    /// Starts the run and streams its updates.
    /// </summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>
    /// The updates, with the tool calls running between them. Their first <c>MoveNextAsync</c> throws an
    /// <see cref="InvalidOperationException"/> when the updates were read before.
    /// </returns>
    public async IAsyncEnumerator<ChatResponseUpdate> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _read, 1) == 1)
            throw new InvalidOperationException("A streaming run streams its updates once; start a new run instead.");

        List<ChatResponseUpdate> updates = [];

        await foreach (var update in _updates.WithCancellation(cancellationToken))
        {
            updates.Add(update);
            yield return update;
        }

        _response = new ChatResponse<TResult>(updates.ToChatResponse(), _serializerOptions);
    }
}
