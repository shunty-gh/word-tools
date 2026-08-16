using Words.Core;

namespace Words.Core.Tests;

public class WordEngineCompositionTests
{
    private static async Task<List<string>> ComposeAsync(
        Lexicon lexicon,
        string letters,
        CompositionOptions? options = null,
        EntryFilter? filter = null)
    {
        var engine = new WordEngine(lexicon);
        var query = new AnagramQuery
        {
            Letters = letters,
            Filter = filter ?? EntryFilter.Default,
            Compose = options ?? CompositionOptions.Default,
        };

        var found = new List<string>();
        await foreach (var match in engine.QueryAsync(query))
        {
            found.Add(match.DisplayForm);
        }

        return found;
    }

    [Fact]
    public async Task AssemblesAnAnswerFromTwoEntries()
    {
        var lexicon = await TestLexicon.OfAsync("cat", "dog", "catdog");

        Assert.Contains("cat dog", await ComposeAsync(lexicon, "catdog"));
    }

    [Fact]
    public async Task StillReturnsSingleEntryAnswers()
    {
        var lexicon = await TestLexicon.OfAsync("cat", "dog", "catdog");

        Assert.Contains("catdog", await ComposeAsync(lexicon, "catdog"));
    }

    [Fact]
    public async Task ProducesEachPartitionOnlyOnce()
    {
        // Without the lowest-letter rule this returns "cat dog" and "dog cat".
        var lexicon = await TestLexicon.OfAsync("cat", "dog");

        var found = await ComposeAsync(lexicon, "catdog");

        Assert.Single(found);
        Assert.Equal("cat dog", found[0]);
    }

    [Fact]
    public async Task ReturnsEveryEntrySharingAComponentsLetters()
    {
        // ACT is both "act" and "cat", so the partition yields two answers.
        var lexicon = await TestLexicon.OfAsync("act", "cat", "dog");

        Assert.Equal(["act dog", "cat dog"], (await ComposeAsync(lexicon, "actdog")).Order());
    }

    [Fact]
    public async Task UsesEveryLetterExactlyOnce()
    {
        var lexicon = await TestLexicon.OfAsync("cat", "dog");

        // One letter short of "catdog", so nothing can be assembled.
        Assert.Empty(await ComposeAsync(lexicon, "catdo"));
    }

    // -- bounds --

    [Fact]
    public async Task UsesTwoComponentsByDefault()
    {
        var lexicon = await TestLexicon.OfAsync("cat", "dog", "emu");

        Assert.Empty(await ComposeAsync(lexicon, "catdogemu"));
    }

    [Fact]
    public async Task AllowsThreeComponentsWhenAsked()
    {
        var lexicon = await TestLexicon.OfAsync("cat", "dog", "emu");

        var found = await ComposeAsync(
            lexicon,
            "catdogemu",
            new CompositionOptions { MaxComponents = 3 });

        Assert.Contains("cat dog emu", found);
    }

    [Fact]
    public async Task RejectsComponentsShorterThanTheMinimum()
    {
        var lexicon = await TestLexicon.OfAsync("at", "cog", "cat", "og");

        // "at" and "og" are two letters, below the default minimum of three.
        Assert.Empty(await ComposeAsync(lexicon, "atcog"));
    }

    [Fact]
    public async Task AllowsShorterComponentsWhenAsked()
    {
        var lexicon = await TestLexicon.OfAsync("at", "cog");

        var found = await ComposeAsync(
            lexicon,
            "atcog",
            new CompositionOptions { MinComponentLength = 2 });

        Assert.Contains("at cog", found);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task RejectsComponentCountsOutsideTheAllowedRange(int components)
    {
        var engine = new WordEngine(await TestLexicon.OfAsync("cat"));

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.QueryAsync(new AnagramQuery
        {
            Letters = "cat",
            Compose = new CompositionOptions { MaxComponents = components },
        }));
    }

    [Fact]
    public async Task RejectsOneLetterComponents()
    {
        var engine = new WordEngine(await TestLexicon.OfAsync("cat"));

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.QueryAsync(new AnagramQuery
        {
            Letters = "cat",
            Compose = new CompositionOptions { MinComponentLength = 1 },
        }));
    }

    // -- component eligibility --

    [Fact]
    public async Task DoesNotComposeOutOfPhrases()
    {
        var lexicon = await TestLexicon.OfAsync("red herring", "cat");

        Assert.Empty(await ComposeAsync(lexicon, "redherringcat"));
    }

    [Fact]
    public async Task DoesNotComposeOutOfProperNouns()
    {
        var lexicon = await TestLexicon.OfAsync("Paris", "cat");

        Assert.Empty(await ComposeAsync(lexicon, "pariscat"));
    }

    [Fact]
    public async Task DoesNotComposeOutOfRacyEntriesByDefault()
    {
        var lexicon = await TestLexicon.Of(
            Entry.Create("rud", 50, Sources.Nediger, isRacy: true),
            Entry.Create("cat", 50, Sources.Esdb));

        Assert.Empty(await ComposeAsync(lexicon, "rudcat"));
    }

    // -- blanks --

    [Fact]
    public async Task ABlankCanFillOutAComponent()
    {
        var lexicon = await TestLexicon.OfAsync("cats", "dog");

        Assert.Contains("cats dog", await ComposeAsync(lexicon, "catdog."));
    }

    [Fact]
    public async Task AllowsOnlyOneBlankWhenComposing()
    {
        var engine = new WordEngine(await TestLexicon.OfAsync("cat"));

        var error = Assert.Throws<QuerySyntaxException>(() => engine.QueryAsync(new AnagramQuery
        {
            Letters = "cat..",
            Compose = CompositionOptions.Default,
        }));

        Assert.Contains("Only one unknown letter", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TwoBlanksAreStillFineWithoutComposition()
    {
        var lexicon = await TestLexicon.OfAsync("cats");

        var engine = new WordEngine(lexicon);
        var matches = engine.QueryAsync(new AnagramQuery { Letters = "ca.." });

        Assert.NotEmpty(await matches.ToListAsync());
    }

    // -- scoring and cancellation --

    [Fact]
    public async Task ACompositionScoresAsItsWeakestComponent()
    {
        var lexicon = await TestLexicon.Of(
            Entry.Create("cat", 90, Sources.Esdb),
            Entry.Create("dog", 30, Sources.Esdb));

        var engine = new WordEngine(lexicon);
        var composition = await engine
            .QueryAsync(new AnagramQuery { Letters = "catdog", Compose = CompositionOptions.Default })
            .FirstAsync(m => m.IsComposition);

        Assert.Equal(30, composition.Score);
    }

    [Fact]
    public async Task StopsWhenCancelled()
    {
        var lexicon = await TestLexicon.OfAsync("cat", "dog", "cats", "dogs");
        var engine = new WordEngine(lexicon);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in engine.QueryAsync(
                new AnagramQuery { Letters = "catdog.", Compose = CompositionOptions.Default },
                cancellation.Token))
            {
            }
        });
    }
}

internal static class AsyncEnumerableTestExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var items = new List<T>();

        await foreach (var item in source)
        {
            items.Add(item);
        }

        return items;
    }

    public static async Task<T> FirstAsync<T>(this IAsyncEnumerable<T> source, Func<T, bool> predicate)
    {
        await foreach (var item in source)
        {
            if (predicate(item))
            {
                return item;
            }
        }

        throw new InvalidOperationException("No matching element.");
    }
}
