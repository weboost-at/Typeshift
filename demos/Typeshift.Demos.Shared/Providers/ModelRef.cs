namespace Typeshift.Demos.Providers;

/// <summary>
/// A model as the app names it: the provider the user defined, then the model id that provider knows, as in
/// <c>lmstudio/qwen/qwen3.8-27b</c>.
/// </summary>
/// <param name="Provider">The provider's name in the catalog.</param>
/// <param name="Model">The model id the provider sends on the wire.</param>
public readonly record struct ModelRef(string Provider, string Model)
{
    // The command line names a model as "provider/model"; model ids carry slashes of their own ("qwen/qwen3.8-27b"),
    // so only the first slash separates
    public static ModelRef Parse(string name) =>
        name.IndexOf('/') is var slash and > 0 && slash < name.Length - 1
            ? new ModelRef(name[..slash], name[(slash + 1)..])
            : throw new ArgumentException($"'{name}' is not a provider/model name.", nameof(name));

    public override string ToString() => $"{Provider}/{Model}";
}
