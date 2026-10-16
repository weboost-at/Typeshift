using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Typeshift.Demos.Providers;

/// <summary>
/// A provider the user defined at runtime: an OpenAI-compatible endpoint, Anthropic, or a canned model for offline
/// runs. <c>${VAR}</c> in the key reads the environment, so the file holds no secrets.
/// </summary>
/// <param name="Name">The name the app's model names start with.</param>
/// <param name="Kind"><c>openai</c>, <c>anthropic</c> or <c>canned</c>.</param>
/// <param name="Endpoint">The base URL of an OpenAI-compatible server; null for the provider's own.</param>
/// <param name="ApiKey">The key, or <c>${VAR}</c>.</param>
public sealed record ProviderDefinition(string Name, string Kind, string? Endpoint = null, string? ApiKey = null);

/// <summary>
/// The providers the user defined, kept in a JSON file the app edits at runtime, and the model runs use unless the
/// settings name another.
/// </summary>
public sealed partial class ProviderCatalog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _path;
    private ProviderFile _file;

    public ProviderCatalog(string path)
    {
        _path = path;
        _file = File.Exists(path) ? JsonSerializer.Deserialize<ProviderFile>(File.ReadAllText(path), Json)! : new("", []);
    }

    // CLUNKY 2: an agent must name a model in code, so the demo's agents name "default", a sentinel this catalog
    // resolves. Typeshift has no notion of a default model or of a model the host chooses at runtime.
    public const string Default = "default";

    public string DefaultModel => _file.Default;

    public IReadOnlyList<ProviderDefinition> Providers => _file.Providers;

    public ProviderDefinition this[string name] =>
        Providers.FirstOrDefault(provider => provider.Name == name)
        ?? throw new InvalidOperationException($"No provider '{name}' is defined; add it to {_path}.");

    public string Resolve(string model) => model == Default ? DefaultModel : model;

    public void Add(ProviderDefinition provider) =>
        Save(_file with { Providers = [.. Providers.Where(known => known.Name != provider.Name), provider] });

    public void SetDefault(string model) => Save(_file with { Default = model });

    // CLUNKY 3: a settings UI needs the models a provider offers, and Typeshift has no model catalog, so the app asks
    // each provider itself; only OpenAI-compatible servers have an endpoint for it
    public async Task<IReadOnlyList<string>> ListModelsAsync(string provider, CancellationToken ct)
    {
        var definition = this[provider];
        if (definition.Kind != "openai") return [];

        using var http = new HttpClient { BaseAddress = new Uri(definition.Endpoint ?? "https://api.openai.com/v1/") };
        http.DefaultRequestHeaders.Authorization = new("Bearer", Expand(definition.ApiKey));

        var models = await http.GetFromJsonAsync<ModelList>("models", Json, ct);
        return [.. (models?.Data ?? []).Select(model => model.Id).Order()];
    }

    public static string Expand(string? value) =>
        value is null ? "" : Variable().Replace(value, match => Environment.GetEnvironmentVariable(match.Groups[1].Value) ?? "");

    private void Save(ProviderFile file)
    {
        _file = file;
        File.WriteAllText(_path, JsonSerializer.Serialize(file, Json));
    }

    [GeneratedRegex(@"\$\{(\w+)\}")]
    private static partial Regex Variable();

    private sealed record ProviderFile(string Default, IReadOnlyList<ProviderDefinition> Providers);

    private sealed record ModelList(IReadOnlyList<ModelEntry> Data);

    private sealed record ModelEntry(string Id);
}
