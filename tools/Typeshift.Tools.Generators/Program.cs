using Typeshift.Tools.Generators;

return args switch
{
    ["measure"] => await Measure(generator: true, analyzer: true),
    ["measure", "generator"] => await Measure(generator: true, analyzer: false),
    ["measure", "analyzer"] => await Measure(generator: false, analyzer: true),
    ["fuzz"] => Fuzz.Run(threads: 2),
    ["fuzz", var given] when int.TryParse(given, out var threads) && threads > 0 => Fuzz.Run(threads),
    _ => Usage()
};

static async Task<int> Measure(bool generator, bool analyzer)
{
    if (generator) GeneratorCost.Print();
    if (analyzer) await AnalyzerCost.Print();

    return 0;
}

static int Usage()
{
    Console.Error.WriteLine(
        """
        Usage: dotnet run --project tools/Typeshift.Tools.Generators -c Release -- <command>

          measure [generator|analyzer]
              What a generator run after an unrelated edit costs for 0 to 100 pairs of a toolset and an agent, and
              what the analyzer costs in code that uses nothing of the library; both unless one is named
          fuzz [threads]
              Runs the generator and the analyzer over every source of the generator tests' catalogs and every docs
              sample, cut after each token of each constructor, on 2 threads unless given
        """);

    return 2;
}
