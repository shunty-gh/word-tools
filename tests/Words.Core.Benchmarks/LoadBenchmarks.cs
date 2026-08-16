using BenchmarkDotNet.Attributes;
using Words.Core;

namespace Words.Core.Benchmarks;

/// <summary>
/// Cold start: what a command-line invocation pays before it can answer anything.
/// </summary>
/// <remarks>
/// The indexes are lazy, so each benchmark loads and then touches one of them. The gap
/// between the first and the others is the cost of that index. A short job because each
/// operation takes a quarter of a second; remove the attribute for a full run.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class LoadBenchmarks
{
    private static ValueTask<Lexicon> LoadAsync() => Lexicon.LoadAsync([EmbeddedLexicon.Source]);

    [Benchmark(Baseline = true, Description = "Load only")]
    public async Task<int> Load() => (await LoadAsync()).Count;

    [Benchmark(Description = "Load + length index (a pattern query)")]
    public async Task<int> LoadAndIndexByLength() => (await LoadAsync()).DistinctLengths;

    [Benchmark(Description = "Load + anagram index (an anagram query)")]
    public async Task<int> LoadAndIndexByCanonicalForm() => (await LoadAsync()).DistinctCanonicalForms;
}
