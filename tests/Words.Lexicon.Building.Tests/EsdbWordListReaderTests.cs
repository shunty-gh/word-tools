using Words.Core;
using Words.Lexicon.Building;

namespace Words.Lexicon.Building.Tests;

public class EsdbWordListReaderTests
{
    private static string ListWithSize(int size) =>
        $"""
         Custom wordlist generated from https://app.aspell.net/create using
         the English Speller Database (ESDB) with parameters:
           Size: {size} (huge)
           Spelling: GB
           Variant Level: 8 (uncommon)
           Diacritics: keep

         cat
         café
         Paris

         """;

    private static List<RawEntry> Read(string content) =>
        new EsdbWordListReader().Read(new StringReader(content)).ToList();

    [Fact]
    public void SkipsTheHeaderAndReadsEntries()
    {
        var entries = Read(ListWithSize(80));

        Assert.Equal(["cat", "café", "Paris"], entries.Select(e => e.DisplayForm));
        Assert.All(entries, e => Assert.Equal(Sources.Esdb, e.Source));
        Assert.All(entries, e => Assert.False(e.IsRacy));
    }

    [Theory]
    [InlineData(35, 100)]
    [InlineData(50, 90)]
    [InlineData(60, 80)]
    [InlineData(70, 65)]
    [InlineData(80, 50)]
    public void ScoresBySizeBandSoSmallerBandsRankHigher(int size, int expectedScore) =>
        Assert.All(Read(ListWithSize(size)), e => Assert.Equal(expectedScore, e.Score));

    [Fact]
    public void ThrowsWhenTheHeaderDeclaresNoSize()
    {
        const string noSize = """
                              Custom wordlist generated from https://app.aspell.net/create using
                              the English Speller Database (ESDB) with parameters:

                              cat

                              """;

        Assert.Throws<InvalidDataException>(() => Read(noSize));
    }

    [Fact]
    public void RecognisesItsOwnFormat() =>
        Assert.True(new EsdbWordListReader().CanRead(
            ["Custom wordlist generated from https://app.aspell.net/create using", "  Size: 80 (huge)"]));

    [Fact]
    public void DoesNotClaimANedigerFile() =>
        Assert.False(new EsdbWordListReader().CanRead(["cat;51", "dog;99"]));
}
