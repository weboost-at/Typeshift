using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Tests.Tools.Support;

// Tools that yield their results one at a time
public partial class StreamingToolset : Toolset
{
    public StreamingToolset()
    {
        AddTool(SplitWordsAsync);
        AddTool(CountSlowlyAsync);
    }

    /// <summary>
    /// Splits a text into its words, one at a time.
    /// </summary>
    /// <param name="text">The text to split.</param>
    public static async IAsyncEnumerable<string> SplitWordsAsync(string text)
    {
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            await Task.Yield();
            yield return word;
        }
    }

    /// <summary>
    /// Counts to three, and waits at its gate once the first number is read. It takes no token.
    /// </summary>
    public static async IAsyncEnumerable<int> CountSlowlyAsync(IServiceProvider services)
    {
        var gate = services.GetRequiredService<StreamGate>();

        for (var number = 1; number <= 3; number++)
        {
            yield return number;

            if (number > 1) continue;

            gate.FirstRead.TrySetResult();
            await gate.Opened.Task;
        }
    }
}

// Where a stream tool waits between two of its items, so a test acts while the stream is being read
public sealed class StreamGate
{
    public TaskCompletionSource FirstRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Opened { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
