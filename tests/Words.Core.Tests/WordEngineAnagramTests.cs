using Words.Core;

namespace Words.Core.Tests;

public class WordEngineAnagramTests
{
    private static async Task<List<string>> QueryAsync(Lexicon lexicon, string letters, EntryFilter? filter = null)
    {
        var engine = new WordEngine(lexicon);
        var query = new AnagramQuery { Letters = letters, Filter = filter ?? EntryFilter.Default };

        var found = new List<string>();
        await foreach (var match in engine.QueryAsync(query))
        {
            found.Add(match.DisplayForm);
        }

        return found;
    }

    [Fact]
    public async Task FindsEveryRearrangementOfTheLetters()
    {
        var lexicon = await TestLexicon.OfAsync("listen", "silent", "tinsel", "cat");

        Assert.Equal(["listen", "silent", "tinsel"], (await QueryAsync(lexicon, "listen")).Order());
    }

    [Fact]
    public async Task IgnoresTheOrderTheLettersWereTypedIn() =>
        Assert.Equal(
            await QueryAsync(await TestLexicon.OfAsync("silent"), "listen"),
            await QueryAsync(await TestLexicon.OfAsync("silent"), "netsil"));

    [Fact]
    public async Task UsesEveryLetterSoShorterWordsDoNotMatch()
    {
        // An exact anagram, not "words you could make from some of these letters".
        var lexicon = await TestLexicon.OfAsync("listen", "list", "ten", "lets");

        Assert.Equal(["listen"], await QueryAsync(lexicon, "listen"));
    }

    [Fact]
    public async Task FindsPhrasesWhoseLettersMatch()
    {
        var lexicon = await TestLexicon.OfAsync("a cat", "acta", "taca");

        Assert.Equal(3, (await QueryAsync(lexicon, "acat")).Count);
    }

    [Fact]
    public async Task ForgivesSpacesAndAccentsInTheInput()
    {
        var lexicon = await TestLexicon.OfAsync("naive");

        Assert.Equal(["naive"], await QueryAsync(lexicon, "naïve"));
        Assert.Equal(["naive"], await QueryAsync(lexicon, "na ive"));
    }

    // -- blanks --

    [Fact]
    public async Task ABlankStandsForAnyOneLetter()
    {
        // "cat" itself is too short once a blank is added, and "cars" is the right length
        // but has no T, so neither can match.
        var lexicon = await TestLexicon.OfAsync("cats", "chat", "cart", "cars", "cat");

        Assert.Equal(["cart", "cats", "chat"], (await QueryAsync(lexicon, "cat?")).Order());
    }

    [Fact]
    public async Task ABlankIsAlwaysUsedSoAnswerLengthIsLettersPlusBlanks()
    {
        var lexicon = await TestLexicon.OfAsync("cat", "cats", "catsu");

        Assert.Equal(["cats"], await QueryAsync(lexicon, "cat."));
        Assert.Equal(["catsu"], await QueryAsync(lexicon, "cat.."));
    }

    [Fact]
    public async Task BlanksMayBeSpeltEitherWay() =>
        Assert.Equal(
            await QueryAsync(await TestLexicon.OfAsync("cats"), "cat?"),
            await QueryAsync(await TestLexicon.OfAsync("cats"), "cat."));

    [Fact]
    public async Task ReturnsEachAnswerOnlyOnceAcrossAllBlankCombinations()
    {
        var lexicon = await TestLexicon.OfAsync("cats", "cast", "acts", "scat");

        var found = await QueryAsync(lexicon, "ca??");

        Assert.Equal(found.Count, found.Distinct().Count());
    }

    [Fact]
    public async Task ThreeBlanksStillResolve()
    {
        var lexicon = await TestLexicon.OfAsync("cats", "abcde");

        Assert.Equal(["abcde"], await QueryAsync(lexicon, "ab???"));
    }

    // -- filtering, shared with pattern queries --

    [Fact]
    public async Task ExcludesRacyEntriesByDefault()
    {
        var lexicon = await TestLexicon.Of(
            Entry.Create("cat", 50, Sources.Esdb),
            Entry.Create("act", 50, Sources.Nediger, isRacy: true));

        Assert.Equal(["cat"], await QueryAsync(lexicon, "cat"));
    }

    [Fact]
    public async Task FiltersByEntryKind()
    {
        var lexicon = await TestLexicon.OfAsync("a cat", "acta");

        Assert.Equal(
            ["acta"],
            await QueryAsync(lexicon, "acat", new EntryFilter { Kinds = EntryKinds.SingleWord }));
    }

    // -- errors and streaming --

    [Fact]
    public async Task ReportsBadLettersWhenCalledRatherThanWhenEnumerated()
    {
        var engine = new WordEngine(await TestLexicon.OfAsync("cat"));

        Assert.Throws<QuerySyntaxException>(
            () => engine.QueryAsync(new AnagramQuery { Letters = "cat????" }));
    }

    [Fact]
    public async Task ReturnsNothingWhenNoAnswerUsesTheseLetters() =>
        Assert.Empty(await QueryAsync(await TestLexicon.OfAsync("cat"), "zzzz"));

    [Fact]
    public async Task StopsWhenCancelled()
    {
        // Three blanks is 3,276 lookups, comfortably past the yield interval at which
        // cancellation is observed.
        var engine = new WordEngine(await TestLexicon.OfAsync("cat"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in engine.QueryAsync(
                new AnagramQuery { Letters = "ab???" }, cancellation.Token))
            {
            }
        });
    }
}
