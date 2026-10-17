using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift;
using Typeshift.Demos.Hosting;
using Typeshift.Demos.MultiProvider;
using Typeshift.Demos.Providers;

// An app whose users define their own model providers at runtime and pick a model per user. It runs offline on the
// canned provider; pass "lmstudio/<model>", "openai/<model>" or "anthropic/<model>" to use a real one.
//
//   dotnet run --project demos/Typeshift.Demos.MultiProvider -- [provider/model] [--trace]
//   dotnet run --project demos/Typeshift.Demos.MultiProvider -- --list lmstudio
//   dotnet run --project demos/Typeshift.Demos.MultiProvider -- --add groq openai https://api.groq.com/openai/v1 ${GROQ_KEY}
//
// Every "CLUNKY n" comment marks a place where the current library makes this harder than it should be.
var catalog = Demo.Catalog();
using var spans = Demo.Flag(args, "--trace") ? new ConsoleSpans() : null;

Demo.Heading("The providers the user defined");

foreach (var definition in catalog.Providers)
    Demo.Note($"{definition.Name,-10} {definition.Kind,-10} {definition.Endpoint ?? "(the provider's own endpoint)"}");

Demo.Note($"default model: {catalog.DefaultModel}");

if (Demo.Option(args, "--add") is { } name)
{
    var at = Array.IndexOf(args, "--add");
    catalog.Add(new ProviderDefinition(name, args[at + 2], args[at + 3], args[at + 4]));
    Demo.Note($"added provider '{name}'");
    return 0;
}

if (Demo.Option(args, "--list") is { } listed)
{
    Demo.Heading($"The models '{listed}' offers, as a settings UI would list them");
    var models = await catalog.ListModelsAsync(listed, CancellationToken.None);
    foreach (var model in models) Demo.Note($"{listed}/{model}");
    if (models.Count == 0) Demo.Note("none: only OpenAI-compatible providers list their models");
    return 0;
}

// The canned model: it asks for the time in the city named, then answers from the result
var canned = new CannedModels
{
    ["echo"] = new ScriptedChatClient(call =>
    {
        var city = call.LastUserText.Contains("Tokyo", StringComparison.OrdinalIgnoreCase) ? "Tokyo" : "Vienna";

        return call.LastIsToolResult
            ? ScriptedCall.Text($"It is {call.LastResults.First().Result} in {city}.")
            : call.Offers("get_time")
                ? ScriptedCall.Call("get_time", ("timeZone", city == "Tokyo" ? "Asia/Tokyo" : "Europe/Vienna"))
                : ScriptedCall.Text("Hello from the canned model.");
    })
};

var services = new ServiceCollection();
services.AddSingleton(TimeProvider.System);
services.AddDemoServices(catalog, canned);

services.AddTypeshift()
    .WithChatClientProvider<RoutingChatClientProvider>()
    .WithRealtimeTracking();

await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateOnBuild = true,
    ValidateScopes = true
});

await using var scope = provider.CreateAsyncScope();
var assistant = scope.ServiceProvider.GetRequiredService<AgentHost<AssistantAgent>>();
var userId = Guid.Parse("00000000-0000-0000-0000-000000000042");
var chosen = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal)) ?? catalog.DefaultModel;

Demo.Heading($"The user picks '{chosen}' in the settings UI");

// CLUNKY 5: ConfigureAsync stores any string; a provider that does not exist, or a model the provider lacks, fails
// at the next run, not here
var settings = await assistant.ConfigureAsync(userId, settings => settings with { Model = chosen });
Demo.Note($"stored Settings.Model = {settings.Model}");

Demo.Heading("A run routes through the user's provider");
var response = await assistant.RunAsync("What time is it in Vienna?", userId);
Console.WriteLine($"{response.Messages[^1].AuthorName}> {response.Text}");
Demo.Note($"the provider reported model '{response.ModelId}' for the name '{chosen}'");

Demo.Heading("One run on another provider, the stored choice untouched");
var oneRun = new AssistantAgent.Settings { Model = "canned/echo" };
var canned1 = await assistant.RunAsync("What time is it in Vienna?", userId, settings: oneRun);
Console.WriteLine($"{canned1.Messages[^1].AuthorName}> {canned1.Text}");
Demo.Note($"stored Settings.Model is still {(await assistant.LoadSettingsAsync(userId)).Data.Model}");

Demo.Heading("Streaming goes through the same provider");
await Demo.StreamAsync(assistant.RunStreamingAsync("And in Tokyo?", userId), CancellationToken.None);

Demo.Heading("A mistyped model fails at run time, not when it was stored");
var mistyped = $"{ModelRef.Parse(chosen).Provider}/does-not-exist";
await assistant.ConfigureAsync(userId, settings => settings with { Model = mistyped });

try
{
    await assistant.RunAsync("What time is it in Vienna?", userId);
    Demo.Note("the provider accepted the model (it may fall back to a default of its own)");
}
catch (Exception exception)
{
    Demo.Note($"{exception.GetType().Name}: {exception.Message.ReplaceLineEndings(" ")[..Math.Min(160, exception.Message.Length)]}");
}

await assistant.ConfigureAsync(userId, settings => settings with { Model = chosen });

Demo.Heading("What a settings UI can offer the user");
var offered = await catalog.ListModelsAsync(ModelRef.Parse(chosen).Provider, CancellationToken.None);
Demo.Note(offered.Count == 0
    ? $"'{ModelRef.Parse(chosen).Provider}' lists no models here: the UI needs a hand-kept list"
    : $"{offered.Count} models, named by the app as {ModelRef.Parse(chosen).Provider}/<id>: {string.Join(", ", offered.Take(4))}…");

Demo.Heading("Where the current library gets in the way");
Demo.Note("1  one string names a model, so the app invents 'provider/model' and parses it everywhere");
Demo.Note("2  an agent must name a model in code, so 'default' is a sentinel the provider resolves");
Demo.Note("3  no model catalog: a settings UI asks each provider itself, and only some can answer");
Demo.Note("4  the provider is scoped but clients must live long: a second singleton holds them");
Demo.Note("5  nothing validates a stored model name; a typo surfaces in the middle of a run");
Demo.Note("6  ChatOptions.ModelId reaches the wire with the composite name: every client is wrapped to put the id back");

return 0;
