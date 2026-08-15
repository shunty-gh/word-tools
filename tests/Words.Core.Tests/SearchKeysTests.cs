using Words.Core;

namespace Words.Core.Tests;

public class SearchKeysTests
{
    [Theory]
    [InlineData("cat", "CAT")]
    [InlineData("Red Herring", "REDHERRING")]
    [InlineData("well-known", "WELLKNOWN")]
    [InlineData("cat's", "CATS")]
    [InlineData("'Allo 'Allo", "ALLOALLO")]
    [InlineData("xiv", "XIV")]
    public void StripsEverythingButLetters(string displayForm, string expected) =>
        Assert.Equal(expected, SearchKeys.From(displayForm));

    [Theory]
    [InlineData("naïve", "NAIVE")]
    [InlineData("café", "CAFE")]
    [InlineData("Ångström", "ANGSTROM")]
    [InlineData("señor", "SENOR")]
    public void FoldsDiacriticsToTheirBaseLetter(string displayForm, string expected) =>
        Assert.Equal(expected, SearchKeys.From(displayForm));

    [Fact]
    public void AccentedAndUnaccentedFormsShareAKey() =>
        Assert.Equal(SearchKeys.From("cafe"), SearchKeys.From("café"));

    [Fact]
    public void EntriesWithNoLettersProduceAnEmptyKey() =>
        Assert.Equal(string.Empty, SearchKeys.From("-'-"));

    [Fact]
    public void AnagramsShareACanonicalForm() =>
        Assert.Equal(
            SearchKeys.ToCanonical(SearchKeys.From("listen")),
            SearchKeys.ToCanonical(SearchKeys.From("silent")));

    [Fact]
    public void NonAnagramsDoNotShareACanonicalForm() =>
        Assert.NotEqual(
            SearchKeys.ToCanonical(SearchKeys.From("listen")),
            SearchKeys.ToCanonical(SearchKeys.From("listed")));

    [Fact]
    public void CanonicalFormIgnoresWordBoundaries() =>
        Assert.Equal(
            SearchKeys.ToCanonical(SearchKeys.From("Red Herring")),
            SearchKeys.ToCanonical(SearchKeys.From("herringred")));
}
