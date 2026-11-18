using System.Text;
using Microsoft.Extensions.AI;
using Typeshift.Tests.Agents.Support;

namespace Typeshift.Tests.Tools.Support;

// Tools that hand the model content, such as an image, in place of JSON
public partial class ContentToolset : Toolset
{
    public const string MediaType = "image/png";

    public ContentToolset()
    {
        AddTool(Picture);
        AddTool(ScanAsync);
        AddTool(Annotate);
    }

    /// <summary>
    /// Draws a caption.
    /// </summary>
    /// <param name="caption">The caption.</param>
    public static DataContent Picture(string caption) => Drawn(caption);

    /// <summary>
    /// Scans a caption.
    /// </summary>
    /// <param name="caption">The caption.</param>
    public static async Task<DataContent> ScanAsync(string caption)
    {
        await Task.Yield();
        return Drawn(caption);
    }

    /// <summary>
    /// Draws a caption and writes it beside.
    /// </summary>
    /// <param name="caption">The caption.</param>
    public static IEnumerable<AIContent> Annotate(string caption)
    {
        yield return new TextContent(caption);
        yield return Drawn(caption);
    }

    // The bytes of a caption's picture are its text, so a test reads which caption was drawn
    public static DataContent Drawn(string caption) => new(Encoding.UTF8.GetBytes(caption), MediaType);
}

// Offers the tools that hand the model content
public partial class ContentAgent : Agent
{
    public ContentAgent()
    {
        WithChatOptions()
            .WithModel(ProbeAgent.ModelProvider, ProbeAgent.Model);

        UseToolset<ContentToolset>();
    }
}
