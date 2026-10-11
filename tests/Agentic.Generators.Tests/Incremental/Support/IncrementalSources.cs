namespace Agentic.Generators.Tests.Incremental.Support;

public static class IncrementalSources
{
    public const string Unrelated =
        """
        namespace Demo;

        public class Unrelated;
        """;

    public const string UnrelatedEdited =
        """
        namespace Demo;

        public class Unrelated
        {
            public int Count { get; set; }
        }
        """;
}
