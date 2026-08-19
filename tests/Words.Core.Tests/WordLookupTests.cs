using Words.Core;

namespace Words.Core.Tests;

public class WordLookupTests
{
    private static readonly WebSearchEngine Google = WordLookup.Find("google");

    [Fact]
    public void DefinitionAsksTheEngineToDefineTheAnswer() =>
        Assert.Equal(
            "https://www.google.com/search?q=define%20otter",
            Google.UrlFor(LookupKind.Definition, "otter"));

    [Fact]
    public void SynonymsAsksTheEngineForSynonyms() =>
        Assert.Equal(
            "https://www.google.com/search?q=otter%20synonyms",
            Google.UrlFor(LookupKind.Synonyms, "otter"));

    [Fact]
    public void LooksUpTheDisplayFormRatherThanTheSearchKey() =>
        Assert.Equal(
            "https://www.google.com/search?q=define%20Red%20Herring",
            Google.UrlFor(LookupKind.Definition, "Red Herring"));

    // Answers are not tidy: they carry accents, apostrophes, hyphens and spaces. Asserted by
    // unescaping rather than against a fixed encoding, because which of those characters
    // need escaping is the escaper's business and has changed between framework versions.
    [Theory]
    [InlineData("naïve")]
    [InlineData("inlet's")]
    [InlineData("well-known")]
    [InlineData("Red Herring")]
    public void EscapesWhateverTheAnswerContains(string displayForm)
    {
        var url = Google.UrlFor(LookupKind.Definition, displayForm);
        var query = url[Google.QueryUrl.Length..];

        Assert.DoesNotContain(" ", query, StringComparison.Ordinal);
        Assert.Equal($"define {displayForm}", Uri.UnescapeDataString(query));
    }

    [Fact]
    public void SurroundingSpaceIsNotPartOfTheAnswer() =>
        Assert.Equal(
            Google.UrlFor(LookupKind.Definition, "otter"),
            Google.UrlFor(LookupKind.Definition, "  otter  "));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RefusesAnAnswerWithNothingToLookUp(string displayForm) =>
        Assert.Throws<ArgumentException>(() => Google.UrlFor(LookupKind.Definition, displayForm));

    [Fact]
    public void EveryEngineProducesAnAbsoluteUrl()
    {
        foreach (var engine in WordLookup.Engines)
        {
            foreach (var kind in Enum.GetValues<LookupKind>())
            {
                var url = engine.UrlFor(kind, "Red Herring");

                Assert.True(
                    Uri.TryCreate(url, UriKind.Absolute, out var uri),
                    $"{engine.Name} produced '{url}', which is not an absolute URL.");
                Assert.Equal(Uri.UriSchemeHttps, uri!.Scheme);
                Assert.Contains("Red%20Herring", url, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void EveryEngineHasItsOwnId()
    {
        var ids = WordLookup.Engines.Select(e => e.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void GoogleIsTheDefault() => Assert.Equal("google", WordLookup.Default.Id);

    [Theory]
    [InlineData("duckduckgo")]
    [InlineData("DuckDuckGo")]
    public void AnEngineIsFoundByIdWhateverItsCase(string id) =>
        Assert.Equal("duckduckgo", WordLookup.Find(id).Id);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("askjeeves")]
    public void AnUnknownChoiceFallsBackToTheDefault(string? id) =>
        Assert.Equal(WordLookup.Default, WordLookup.Find(id));
}
