using Words.Core;

namespace Words.Core.Tests;

public class LookupSitesTests
{
    private static LookupSite Site(string name) => LookupSites.ByName(name);

    [Fact]
    public void DefinitionAsksTheEngineToDefineTheAnswer()
    {
        var uri = Site("Google").UriFor(LookupKind.Definition, "herring");

        Assert.Equal("https://www.google.com/search?q=define%20herring", uri.AbsoluteUri);
    }

    [Fact]
    public void SynonymsAsksTheEngineForSynonymsOfTheAnswer()
    {
        var uri = Site("Google").UriFor(LookupKind.Synonyms, "herring");

        Assert.Equal("https://www.google.com/search?q=herring%20synonyms", uri.AbsoluteUri);
    }

    [Fact]
    public void EachSiteKeepsItsOwnQueryParameter()
    {
        // Not every engine calls it 'q', which is why the whole prefix is held.
        Assert.Equal("https://search.yahoo.com/search?p=define%20cat", Site("Yahoo").UriFor(LookupKind.Definition, "cat").AbsoluteUri);
        Assert.Equal("https://www.startpage.com/sp/search?query=define%20cat", Site("Startpage").UriFor(LookupKind.Definition, "cat").AbsoluteUri);
    }

    [Fact]
    public void PhrasesAndPunctuationSurviveEscaping()
    {
        // A phrase entry, and a possessive: both are real answers, and neither may break the
        // URL or arrive as a different search.
        var phrase = Site("Bing").UriFor(LookupKind.Definition, "Red Herring");
        var possessive = Site("Bing").UriFor(LookupKind.Definition, "inlet's");

        Assert.Equal("https://www.bing.com/search?q=define%20Red%20Herring", phrase.AbsoluteUri);
        Assert.Equal("https://www.bing.com/search?q=define%20inlet%27s", possessive.AbsoluteUri);
    }

    [Fact]
    public void AccentsSurviveEscaping()
    {
        // The display form keeps its accents, and it is the display form we look up.
        var uri = Site("DuckDuckGo").UriFor(LookupKind.Definition, "naïve");

        Assert.Equal("https://duckduckgo.com/?q=define%20na%C3%AFve", uri.AbsoluteUri);
    }

    [Fact]
    public void EverySiteBuildsAnAbsoluteHttpsUrl()
    {
        foreach (var site in LookupSites.All)
        {
            foreach (var kind in Enum.GetValues<LookupKind>())
            {
                var uri = site.UriFor(kind, "Red Herring");

                Assert.True(uri.IsAbsoluteUri, $"{site.Name} produced a relative URL.");
                Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
            }
        }
    }

    [Fact]
    public void AnAnswerWithNoLettersIsRefused()
    {
        Assert.Throws<ArgumentException>(() => LookupSites.Default.UriFor(LookupKind.Definition, "   "));
    }

    [Fact]
    public void GoogleIsTheDefault()
    {
        Assert.Equal("Google", LookupSites.Default.Name);
        Assert.Equal(0, LookupSites.IndexOf(LookupSites.Default));
    }

    [Fact]
    public void SitesAreNamedUniquely()
    {
        // The name is what gets stored, so two sites sharing one would make the setting
        // ambiguous.
        Assert.Equal(LookupSites.All.Count, LookupSites.All.Select(site => site.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("duckduckgo")]
    [InlineData("DUCKDUCKGO")]
    public void ASavedNameIsMatchedWhateverItsCase(string saved)
    {
        Assert.Equal("DuckDuckGo", LookupSites.ByName(saved).Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Altavista")]
    public void AnUnknownSavedNameFallsBackToTheDefault(string? saved)
    {
        Assert.Equal(LookupSites.Default, LookupSites.ByName(saved));
    }

    [Fact]
    public void EverySiteCanBeFoundByItsOwnName()
    {
        foreach (var site in LookupSites.All)
        {
            Assert.Equal(site, LookupSites.ByName(site.Name));
            Assert.Equal(site, LookupSites.All[LookupSites.IndexOf(site)]);
        }
    }
}
