using BenchmarkDotNet.Attributes;
using Words.Core;

namespace Words.Core.Benchmarks;

/// <summary>
/// Warm queries, against an already-loaded lexicon with both indexes built. This is what a
/// long-running front end pays per query, and what an interactive session would feel.
/// </summary>
[MemoryDiagnoser]
public class QueryBenchmarks
{
    private WordEngine _engine = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        var lexicon = await Lexicon.LoadAsync([EmbeddedLexicon.Source]);

        // Force both lazy indexes, so these measure querying rather than first-use building.
        _ = lexicon.DistinctLengths;
        _ = lexicon.DistinctCanonicalForms;

        _engine = new WordEngine(lexicon);
    }

    private static async Task<int> CountAsync(IAsyncEnumerable<Match> matches)
    {
        var count = 0;

        await foreach (var _ in matches)
        {
            count++;
        }

        return count;
    }

    // A pattern's cost is the size of its length bucket, not how specific it looks. The
    // buckets peak around nine letters (58k entries) and fall away sharply at both ends,
    // so a long pattern is far more work than a short one.
    [Benchmark(Description = "pattern A.....R.E.T (11 letters, 50k candidates)")]
    public Task<int> PatternLong() =>
        CountAsync(_engine.QueryAsync(new PatternQuery { Pattern = "A.....R.E.T" }));

    [Benchmark(Description = "pattern C.T (3 letters, 3.4k candidates)")]
    public Task<int> PatternShort() =>
        CountAsync(_engine.QueryAsync(new PatternQuery { Pattern = "C.T" }));

    [Benchmark(Description = "anagram listen (no blanks, one lookup)")]
    public Task<int> Anagram() =>
        CountAsync(_engine.QueryAsync(new AnagramQuery { Letters = "listen" }));

    [Benchmark(Description = "anagram trisec. (one blank, 26 lookups)")]
    public Task<int> AnagramOneBlank() =>
        CountAsync(_engine.QueryAsync(new AnagramQuery { Letters = "trisec." }));

    [Benchmark(Description = "anagram ab??? (three blanks, 3,276 lookups)")]
    public Task<int> AnagramThreeBlanks() =>
        CountAsync(_engine.QueryAsync(new AnagramQuery { Letters = "ab???" }));

    [Benchmark(Description = "compose notaproblem (11 letters, 2 words)")]
    public Task<int> ComposeTwoWords() =>
        CountAsync(_engine.QueryAsync(new AnagramQuery
        {
            Letters = "notaproblem",
            Compose = CompositionOptions.Default,
        }));

    [Benchmark(Description = "compose encyclopaedias, 3 words (the pathological one)")]
    public Task<int> ComposeThreeWords() =>
        CountAsync(_engine.QueryAsync(new AnagramQuery
        {
            Letters = "encyclopaedias",
            Compose = new CompositionOptions { MaxComponents = 3 },
        }));
}
