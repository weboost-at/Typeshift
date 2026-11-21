namespace Typeshift.Samples.PackageConsumer;

// A toolset on its way out. The generated files name it too, and this build, which treats warnings as errors, proves
// they repeat no CS0618. It carries no XML docs: the docs the package collects would name it once more
[Obsolete("Use GreetingToolset.")]
internal partial class FarewellToolset : Toolset
{
    public FarewellToolset() => AddTool(Farewell).WithDescription("Says goodbye to a person.");

    public static string Farewell(string name) => $"Goodbye, {name}!";
}
