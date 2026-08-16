using Words.Core;

namespace Words.Core.Tests;

public class WordEnginePatternTests
{
    private static async Task<List<string>> QueryAsync(Lexicon lexicon, string pattern, EntryFilter? filter = null)
    {
        var engine = new WordEngine(lexicon);
        var query = new PatternQuery { Pattern = pattern, Filter = filter ?? EntryFilter.Default };

        var found = new List<string>();
        await foreach (var match in engine.QueryAsync(query))
        {
            found.Add(match.DisplayForm);
        }

        return found;
    }

    [Fact]
    public async Task ReturnsOnlyEntriesMatchingThePattern()
    {
        var lexicon = await TestLexicon.OfAsync("cat", "cot", "cut", "dog");

        Assert.Equal(["cat", "cot", "cut"], (await QueryAsync(lexicon, "C?T")).Order());
    }

    [Fact]
    public async Task ReturnsOnlyAnswersOfThePatternsLength()
    {
        var lexicon = await TestLexicon.OfAsync("cat", "cart", "ca");

        Assert.Equal(["cat"], await QueryAsync(lexicon, "???"));
    }

    [Fact]
    public async Task MatchesPhraseEntriesOnTheirLettersAlone()
    {
        // A grid has no spaces, so an 10-cell answer can be "red herring".
        var lexicon = await TestLexicon.OfAsync("red herring", "redherring", "cat");

        Assert.Equal(["red herring", "redherring"], (await QueryAsync(lexicon, "RED?ERRING")).Order());
    }

    [Fact]
    public async Task IncludesPhrasesAndProperNounsByDefault()
    {
        // PARIS, PAIRS and ATRIP — five letters each once spaces are stripped.
        var lexicon = await TestLexicon.OfAsync("Paris", "pairs", "a trip");

        Assert.Equal(3, (await QueryAsync(lexicon, "?????")).Count);
    }

    [Fact]
    public async Task HonoursCharacterClasses()
    {
        var lexicon = await TestLexicon.OfAsync("cat", "cot", "cut");

        Assert.Equal(["cat", "cot"], (await QueryAsync(lexicon, "C[ao]T")).Order());
        Assert.Equal(["cut"], await QueryAsync(lexicon, "C[^ao]T"));
    }

    [Fact]
    public async Task ExcludesRacyEntriesByDefault()
    {
        var lexicon = await TestLexicon.Of(
            Entry.Create("cat", 50, Sources.Esdb),
            Entry.Create("rud", 50, Sources.Nediger, isRacy: true));

        Assert.Equal(["cat"], await QueryAsync(lexicon, "???"));
    }

    [Fact]
    public async Task AdmitsRacyEntriesWhenAsked()
    {
        var lexicon = await TestLexicon.Of(
            Entry.Create("cat", 50, Sources.Esdb),
            Entry.Create("rud", 50, Sources.Nediger, isRacy: true));

        var found = await QueryAsync(lexicon, "???", new EntryFilter { IncludeRacy = true });

        Assert.Equal(2, found.Count);
    }

    [Fact]
    public async Task FiltersByEntryKind()
    {
        var lexicon = await TestLexicon.OfAsync("a cat", "acta");

        var singleWordsOnly = new EntryFilter { Kinds = EntryKinds.SingleWord };

        Assert.Equal(["acta"], await QueryAsync(lexicon, "????", singleWordsOnly));
    }

    [Fact]
    public async Task FiltersByProvenance()
    {
        var lexicon = await TestLexicon.Of(
            Entry.Create("cat", 50, Sources.Esdb),
            Entry.Create("cot", 50, Sources.Nediger));

        var esdbOnly = new EntryFilter { Sources = Sources.Esdb };

        Assert.Equal(["cat"], await QueryAsync(lexicon, "C?T", esdbOnly));
    }

    [Fact]
    public async Task ReturnsNothingWhenNothingFits() =>
        Assert.Empty(await QueryAsync(await TestLexicon.OfAsync("cat"), "ZZZZZZZZ"));

    [Fact]
    public async Task ReportsSyntaxErrorsWhenCalledRatherThanWhenEnumerated()
    {
        // The whole point of compiling eagerly: a typo surfaces at the call, not later at
        // some unrelated `await foreach`.
        var engine = new WordEngine(await TestLexicon.OfAsync("cat"));

        Assert.Throws<PatternSyntaxException>(
            () => engine.QueryAsync(new PatternQuery { Pattern = "A*D" }));
    }

    [Fact]
    public async Task StreamsSoAConsumerCanStopEarly()
    {
        var lexicon = await TestLexicon.OfAsync("cat", "cot", "cut");
        var engine = new WordEngine(lexicon);

        var taken = 0;
        await foreach (var _ in engine.QueryAsync(new PatternQuery { Pattern = "C?T" }))
        {
            if (++taken == 2)
            {
                break;
            }
        }

        Assert.Equal(2, taken);
    }

    /// <summary>Distinct four-letter display forms, so every entry lands in one bucket.</summary>
    private static string FourLetters(int index)
    {
        Span<char> letters = stackalloc char[4];

        for (var position = 3; position >= 0; position--)
        {
            letters[position] = (char)('a' + (index % 26));
            index /= 26;
        }

        return new string(letters);
    }

    [Fact]
    public async Task StopsWhenCancelled()
    {
        // Must exceed the engine's yield interval, which is the only point at which
        // cancellation is observed — a handful of entries would never check.
        var entries = Enumerable.Range(0, 30_000)
            .Select(i => Entry.Create(FourLetters(i), 50, Sources.Esdb))
            .ToArray();

        var engine = new WordEngine(await TestLexicon.Of(entries));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in engine.QueryAsync(
                new PatternQuery { Pattern = "????" }, cancellation.Token))
            {
            }
        });
    }
}
