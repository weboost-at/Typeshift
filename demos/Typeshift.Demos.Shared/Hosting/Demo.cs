using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Demos.Providers;

namespace Typeshift.Demos.Hosting;

/// <summary>
/// What every demo's host registers: the user-defined providers from <c>providers.json</c>, the store under
/// <c>.typeshift/</c>, and the canned models the demo brings.
/// </summary>
public static class Demo
{
    public static string DataDirectory => Path.Combine(Directory.GetCurrentDirectory(), ".typeshift");

    public static ProviderCatalog Catalog() => new(Path.Combine(AppContext.BaseDirectory, "providers.json"));

    // FRICTION: this library cannot call AddTypeshift() for its hosts, since only the generator of the calling project
    // intercepts the call (D1), so each demo calls it next to this and names the provider itself. The store is
    // registered here as ITypeshiftStore<Guid> directly, since WithStore<T>() takes a type the container must be able
    // to construct, and this store takes a directory.
    public static IServiceCollection AddDemoServices(
        this IServiceCollection services,
        ProviderCatalog catalog,
        CannedModels canned)
    {
        services.AddSingleton(catalog);
        services.AddSingleton(canned);
        services.AddSingleton<ClientCache>();
        services.AddSingleton(new JsonFileStore(DataDirectory));
        services.AddSingleton<ITypeshiftStore<Guid>>(provider => provider.GetRequiredService<JsonFileStore>());

        return services;
    }

    public static bool Flag(string[] args, string flag) => args.Contains(flag, StringComparer.Ordinal);

    public static string? Option(string[] args, string option) =>
        Array.IndexOf(args, option) is var at and >= 0 && at + 1 < args.Length ? args[at + 1] : null;

    public static void Heading(string text)
    {
        Console.WriteLine();
        Console.WriteLine($"== {text}");
    }

    public static void Note(string text) => Console.WriteLine($"   {text}");

    // The lines a reader yields until the token is cancelled, which ends the sequence instead of throwing
    public static async IAsyncEnumerable<string> LinesUntilAsync(
        System.Threading.Channels.ChannelReader<string> reader,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (true)
        {
            bool ready;

            try
            {
                ready = await reader.WaitToReadAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                ready = false;
            }

            if (!ready) yield break;

            while (reader.TryRead(out var line))
            {
                yield return line;
            }
        }
    }

    public static async Task<string> StreamAsync(IAsyncEnumerable<ChatResponseUpdate> updates, CancellationToken ct)
    {
        var text = new System.Text.StringBuilder();
        string? author = null;

        await foreach (var update in updates.WithCancellation(ct))
        {
            if (update.AuthorName is { } name && name != author)
            {
                author = name;
                Console.Write($"\n{author}> ");
            }

            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case TextContent chunk:
                        Console.Write(chunk.Text);
                        text.Append(chunk.Text);
                        break;

                    case FunctionCallContent call:
                        Console.Write($"\n   [call] {call.Name}({Arguments(call)})\n");
                        break;

                    case FunctionResultContent result:
                        Console.Write($"   [result] {Shorten(result.Result?.ToString() ?? "")}\n");
                        break;

                    case ToolApprovalRequestContent request when request.ToolCall is FunctionCallContent asked:
                        Console.Write($"\n   [approval needed] {asked.Name}({Arguments(asked)})\n");
                        break;
                }
            }
        }

        Console.WriteLine();
        return text.ToString();
    }

    public static string Arguments(FunctionCallContent call) =>
        string.Join(", ", (call.Arguments ?? new Dictionary<string, object?>()).Select(argument =>
            $"{argument.Key}: {Shorten(argument.Value?.ToString() ?? "null")}"));

    private static string Shorten(string text)
    {
        var line = text.ReplaceLineEndings(" ");
        return line.Length <= 100 ? line : line[..100] + "…";
    }
}
