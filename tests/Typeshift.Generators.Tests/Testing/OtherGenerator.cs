using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Typeshift.Generators.Tests.Testing;

// Another source generator of the project, which writes one file. Not as post-initialization output, which every
// generator sees: this file reaches the compiler and the analyzers, never Typeshift's generator
public sealed class OtherGenerator(string hintName, string source) : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        context.RegisterSourceOutput(
            context.CompilationProvider,
            (output, _) => output.AddSource(hintName, SourceText.From(source, Encoding.UTF8)));
}
