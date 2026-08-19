using Words.Core;

namespace Words.Core.Tests;

public class SearchEngineTests
{
    [Fact]
    public void TheDefaultIsGoogle() =>
        Assert.Equal("google", SearchEngine.Default.Id);

    [Fact]
    public void TheDefaultIsOffered() =>
        Assert.Contains(SearchEngine.Default, SearchEngine.All);

    [Fact]
    public void EveryEngineHasItsOwnIdentifier()
    {
        // A repeated identifier would make ById ambiguous, and a saved preference would
        // resolve to whichever came first.
        var ids = SearchEngine.All.Select(e => e.Id).ToList();
        var distinct = ids.Distinct().ToList();

        Assert.Equal(ids, distinct);
    }

    [Fact]
    public void EveryEngineIsAskedOverHttps() =>
        Assert.All(SearchEngine.All, e => Assert.StartsWith("https://", e.QueryPrefix, StringComparison.Ordinal));

    [Fact]
    public void EveryEngineTakesItsQueryLast() =>
        Assert.All(SearchEngine.All, e => Assert.EndsWith("=", e.QueryPrefix, StringComparison.Ordinal));

    [Fact]
    public void AnIdentifierFindsItsEngine() =>
        Assert.Equal(SearchEngine.DuckDuckGo, SearchEngine.ById("duckduckgo"));

    [Fact]
    public void AnIdentifierIsFoundWhateverItsCase() =>
        Assert.Equal(SearchEngine.Bing, SearchEngine.ById("BING"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("altavista")]
    public void AnUnrecognisedIdentifierFallsBackToTheDefault(string? id) =>
        Assert.Equal(SearchEngine.Default, SearchEngine.ById(id));

    [Fact]
    public void ADefinitionAsksTheEngineToDefineTheAnswer() =>
        Assert.Equal("https://www.google.com/search?q=define%20anagram", Url("anagram", LookupKind.Definition));

    [Fact]
    public void SynonymsAskTheEngineForSynonymsOfTheAnswer() =>
        Assert.Equal("https://www.google.com/search?q=anagram%20synonyms", Url("anagram", LookupKind.Synonyms));

    [Fact]
    public void EachEngineKeepsItsOwnQueryParameter()
    {
        // q, p and query: the parameter is not the same everywhere, which is the whole
        // reason an engine carries a prefix rather than a host.
        Assert.Equal(
            "https://duckduckgo.com/?q=define%20cat",
            SearchEngine.DuckDuckGo.UrlFor("cat", LookupKind.Definition));
        Assert.Equal(
            "https://search.yahoo.com/search?p=define%20cat",
            SearchEngine.Yahoo.UrlFor("cat", LookupKind.Definition));
        Assert.Equal(
            "https://www.startpage.com/sp/search?query=define%20cat",
            SearchEngine.Startpage.UrlFor("cat", LookupKind.Definition));
    }

    [Fact]
    public void APhraseKeepsItsSpaces() =>
        Assert.Equal("https://www.google.com/search?q=define%20red%20herring", Url("red herring", LookupKind.Definition));

    /// <summary>
    /// The display form is what a dictionary is indexed under, so it goes as it is — which
    /// means anything in it that a URL would read as structure has to be escaped.
    /// </summary>
    [Fact]
    public void AnApostropheIsEscapedRatherThanSentRaw()
    {
        var url = Url("inlet's", LookupKind.Definition);

        Assert.DoesNotContain("'", url, StringComparison.Ordinal);
        Assert.Contains("inlet%27s", url, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAccentIsEscapedRatherThanSentRaw()
    {
        var url = Url("naïve", LookupKind.Definition);

        Assert.DoesNotContain("ï", url, StringComparison.Ordinal);
        Assert.Contains("na%C3%AFve", url, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAmpersandCannotSmuggleInAnotherParameter()
    {
        var url = Url("rock & roll", LookupKind.Definition);

        Assert.DoesNotContain("&", url, StringComparison.Ordinal);
    }

    [Fact]
    public void SurroundingSpaceIsDropped() =>
        Assert.Equal(Url("cat", LookupKind.Definition), Url("  cat  ", LookupKind.Definition));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyAnswerIsAMistakeWorthHearingAbout(string displayForm) =>
        Assert.Throws<ArgumentException>(() => { Url(displayForm, LookupKind.Definition); });

    [Fact]
    public void AnEngineReadsAsItsName() =>
        Assert.Equal("DuckDuckGo", SearchEngine.DuckDuckGo.ToString());

    private static string Url(string displayForm, LookupKind kind) =>
        SearchEngine.Default.UrlFor(displayForm, kind);
}
