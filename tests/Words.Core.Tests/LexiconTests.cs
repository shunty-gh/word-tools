using Words.Core;

namespace Words.Core.Tests;

public class LexiconTests
{
    private sealed class FakeSource(string name, params Entry[] entries) : ILexiconSource
    {
        public string Name { get; } = name;

        public ValueTask<IReadOnlyList<Entry>> LoadAsync(CancellationToken cancellationToken = default) =>
            new((IReadOnlyList<Entry>)entries);
    }

    private static ValueTask<Lexicon> LoadAsync(params ILexiconSource[] sources) =>
        Lexicon.LoadAsync(sources);

    [Fact]
    public async Task LoadsEntriesFromASingleSource()
    {
        var lexicon = await LoadAsync(new FakeSource(
            "a",
            Entry.Create("cat", 50, Sources.Esdb),
            Entry.Create("dog", 50, Sources.Esdb)));

        Assert.Equal(2, lexicon.Count);
        Assert.Equal(["a"], lexicon.SourceNames);
    }

    [Fact]
    public async Task MergesIdenticalDisplayFormsAcrossSources()
    {
        var lexicon = await LoadAsync(
            new FakeSource("a", Entry.Create("cat", 40, Sources.Esdb)),
            new FakeSource("b", Entry.Create("cat", 90, Sources.Personal)));

        var cat = Assert.Single(lexicon.Entries);

        Assert.Equal(90, cat.Score);
        Assert.Equal(Sources.Esdb | Sources.Personal, cat.Sources);
    }

    [Fact]
    public async Task KeepsDistinctDisplayFormsThatShareASearchKey()
    {
        var lexicon = await LoadAsync(
            new FakeSource("a", Entry.Create("Polish", 50, Sources.Esdb)),
            new FakeSource("b", Entry.Create("polish", 50, Sources.Personal)));

        Assert.Equal(2, lexicon.Count);
        Assert.Equal(2, lexicon.OfLength(6).Count);
    }

    [Fact]
    public async Task LaterSourcesCanAddEntries()
    {
        var lexicon = await LoadAsync(
            new FakeSource("a", Entry.Create("cat", 50, Sources.Esdb)),
            new FakeSource("b", Entry.Create("supercalifragilistic", 90, Sources.Personal)));

        Assert.Equal(2, lexicon.Count);
        Assert.Contains(lexicon.Entries, e => e.Sources == Sources.Personal);
    }

    [Fact]
    public async Task EmptySourcesStillCountAsSourcesButAddNothing()
    {
        var lexicon = await LoadAsync(
            new FakeSource("a", Entry.Create("cat", 50, Sources.Esdb)),
            new FakeSource("empty"));

        Assert.Equal(1, lexicon.Count);
        Assert.Equal(["a", "empty"], lexicon.SourceNames);
    }

    [Fact]
    public async Task IndexesEntriesByTheirSearchKeyLength()
    {
        var lexicon = await LoadAsync(new FakeSource(
            "a",
            Entry.Create("cat", 50, Sources.Esdb),          // 3
            Entry.Create("dog", 50, Sources.Esdb),          // 3
            Entry.Create("Red Herring", 50, Sources.Nediger))); // 10, spaces removed

        Assert.Equal(2, lexicon.OfLength(3).Count);
        Assert.Equal("Red Herring", Assert.Single(lexicon.OfLength(10)).DisplayForm);
        Assert.Empty(lexicon.OfLength(99));
    }

    [Fact]
    public async Task IndexesAnagramsTogetherRegardlessOfLetterOrder()
    {
        var lexicon = await LoadAsync(new FakeSource(
            "a",
            Entry.Create("listen", 50, Sources.Esdb),
            Entry.Create("silent", 50, Sources.Esdb),
            Entry.Create("tinsel", 50, Sources.Esdb),
            Entry.Create("cat", 50, Sources.Esdb)));

        var anagrams = lexicon.WithCanonicalForm(SearchKeys.ToCanonical("LISTEN"));

        Assert.Equal(3, anagrams.Count);
        Assert.Empty(lexicon.WithCanonicalForm(SearchKeys.ToCanonical("ZZZZZZ")));
    }

    [Fact]
    public async Task PhrasesAreAnagramsOfSingleWordsWhenTheirLettersMatch()
    {
        var lexicon = await LoadAsync(new FakeSource(
            "a",
            Entry.Create("a cat", 50, Sources.Nediger),
            Entry.Create("acta", 50, Sources.Esdb)));

        Assert.Equal(2, lexicon.WithCanonicalForm(SearchKeys.ToCanonical("ACAT")).Count);
    }
}
