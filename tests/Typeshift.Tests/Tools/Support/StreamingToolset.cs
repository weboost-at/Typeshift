namespace Typeshift.Tests.Tools.Support;

// A tool that yields its results one at a time
public partial class StreamingToolset : Toolset
{
    public StreamingToolset()
    {
        AddTool(SplitWordsAsync);
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
}
