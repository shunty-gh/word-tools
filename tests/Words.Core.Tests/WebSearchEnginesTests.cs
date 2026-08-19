using Words.Core;

namespace Words.Core.Tests;

public class WebSearchEnginesTests
{
    /// <summary>
    /// The address exactly as it was built. <see cref="Uri.ToString"/> is deliberately not
    /// used: it unescapes for display, so a test written against it would pass on an address
    /// that no longer carries what was encoded into it.
    /// </summary>
    private static string Url(LookupKind kind, string displayForm, string engine = "Google") =>
        WebSearchEngines.ByName(engine).UrlFor(kind, displayForm).OriginalString;

    /// <summary>What the engine will actually search for, read back out of the address.</summary>
    private static string TermsIn(Uri url) =>
        Uri.UnescapeDataString(url.OriginalString[(url.OriginalString.IndexOf('=') + 1)..]);

    [Fact]
    public void GoogleIsTheDefault() =>
        Assert.Equal("Google", WebSearchEngines.Default.Name);

    [Fact]
    public void TheDefaultIsTheFirstEngineOffered() =>
        Assert.Same(WebSearchEngines.All[0], WebSearchEngines.Default);

    [Fact]
    public void EveryEngineIsOfferedOnlyOnce() =>
        Assert.Equal(
            WebSearchEngines.All.Count,
            WebSearchEngines.All.Select(e => e.Name).Distinct(StringComparer.Ordinal).Count());

    [Theory]
    [InlineData("Google")]
    [InlineData("Bing")]
    [InlineData("Brave")]
    [InlineData("DuckDuckGo")]
    [InlineData("Ecosia")]
    [InlineData("Startpage")]
    [InlineData("Yahoo")]
    public void TheEnginesAskedForAreAllOffered(string name) =>
        Assert.Equal(name, WebSearchEngines.ByName(name).Name);

    [Fact]
    public void EveryEngineProducesAnHttpsAddressCarryingTheTerms()
    {
        foreach (var engine in WebSearchEngines.All)
        {
            var url = engine.UrlFor(LookupKind.Definition, "otter");

            Assert.Equal(Uri.UriSchemeHttps, url.Scheme);
            Assert.Equal("define otter", TermsIn(url));
        }
    }

    [Fact]
    public void ADefinitionAsksForOne() =>
        Assert.Equal("https://www.google.com/search?q=define%20otter", Url(LookupKind.Definition, "otter"));

    [Fact]
    public void SynonymsAskForThose() =>
        Assert.Equal("https://www.google.com/search?q=otter%20synonyms", Url(LookupKind.Synonyms, "otter"));

    [Fact]
    public void EachEngineUsesItsOwnParameterName()
    {
        // Startpage and Yahoo do not call it "q", which is the only reason the whole
        // address is held rather than a host.
        Assert.Contains("?query=", Url(LookupKind.Definition, "otter", "Startpage"), StringComparison.Ordinal);
        Assert.Contains("?p=", Url(LookupKind.Definition, "otter", "Yahoo"), StringComparison.Ordinal);
    }

    [Fact]
    public void APhraseIsLookedUpWhole() =>
        Assert.Equal(
            "https://www.google.com/search?q=define%20Red%20Herring",
            Url(LookupKind.Definition, "Red Herring"));

    [Fact]
    public void TheDisplayFormIsSearchedForAccentsAndAll()
    {
        // Not the search key: someone looking up "naïve" means that word, not "NAIVE".
        var url = WebSearchEngines.Default.UrlFor(LookupKind.Definition, "naïve");

        Assert.Equal("define naïve", TermsIn(url));
    }

    [Fact]
    public void AnApostropheSurvivesEncoding()
    {
        var url = WebSearchEngines.Default.UrlFor(LookupKind.Synonyms, "inlet's");

        Assert.Equal("inlet's synonyms", TermsIn(url));
    }

    [Fact]
    public void SurroundingSpaceIsIgnored() =>
        Assert.Equal(Url(LookupKind.Definition, "otter"), Url(LookupKind.Definition, "  otter  "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ThereIsNothingToLookUpWithoutAnAnswer(string? displayForm) =>
        Assert.ThrowsAny<ArgumentException>(
            () => WebSearchEngines.Default.UrlFor(LookupKind.Definition, displayForm!));

    [Fact]
    public void AnUnknownStoredNameFallsBackToTheDefault() =>
        Assert.Same(WebSearchEngines.Default, WebSearchEngines.ByName("Altavista"));

    [Fact]
    public void NoStoredNameFallsBackToTheDefault() =>
        Assert.Same(WebSearchEngines.Default, WebSearchEngines.ByName(null));

    [Fact]
    public void AnUnknownStoredNameSelectsTheDefaultsPosition() =>
        Assert.Equal(0, WebSearchEngines.IndexOf("Altavista"));

    [Fact]
    public void EveryEngineRoundTripsThroughItsStoredName()
    {
        // Storing the name rather than the index is what lets the list be reordered without
        // silently changing somebody's saved choice.
        for (var i = 0; i < WebSearchEngines.All.Count; i++)
        {
            var engine = WebSearchEngines.All[i];

            Assert.Same(engine, WebSearchEngines.ByName(engine.Name));
            Assert.Equal(i, WebSearchEngines.IndexOf(engine.Name));
        }
    }
}
