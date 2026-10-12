namespace Typeshift.Tests.Testing.Conventions;

// A [Fact] or [Theory] as the sources declare it, File is "<Project>/<path in the project>"
public sealed record TestMethod(string File, string Namespace, string Class, string Name);
