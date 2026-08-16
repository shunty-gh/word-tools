using Words.Core;
using Words.LexiconBuilding;

namespace Words.LexiconBuilding.Tests;

public class NedigerWordListReaderTests
{
    private static List<RawEntry> Read(string content) =>
        new NedigerWordListReader().Read(new StringReader(content)).ToList();

    [Fact]
    public void ReadsEntryAndScore()
    {
        var entries = Read("cat;51\nRed Herring;99\n");

        Assert.Equal(2, entries.Count);
        Assert.Equal("cat", entries[0].DisplayForm);
        Assert.Equal("Red Herring", entries[1].DisplayForm);
        Assert.All(entries, e => Assert.Equal(Sources.Nediger, e.Source));
    }

    [Fact]
    public void ToleratesCrlfLineEndings()
    {
        // The real file is CRLF-terminated throughout.
        var entries = Read("cat;51\r\ndog;99\r\n");

        Assert.Equal(["cat", "dog"], entries.Select(e => e.DisplayForm));
    }

    [Fact]
    public void ToleratesTrailingWhitespace()
    {
        // A handful of lines in the real file carry a stray tab or space after the score.
        var entries = Read("cat;51\t\ndog;99 \n");

        Assert.Equal(["cat", "dog"], entries.Select(e => e.DisplayForm));
    }

    [Fact]
    public void SplitsOnTheLastSeparatorSoEntriesMayContainSemicolons()
    {
        var entries = Read("a;b;51\n");

        Assert.Equal("a;b", Assert.Single(entries).DisplayForm);
    }

    [Fact]
    public void FlagsTheRacyBandWithoutPenalisingItsScore()
    {
        var racy = Assert.Single(Read("rude;49\n"));

        Assert.True(racy.IsRacy);
        Assert.Equal(50, racy.Score);
    }

    [Theory]
    [InlineData(99, 90)]
    [InlineData(51, 60)]
    [InlineData(25, 25)]
    public void MapsScoresOntoTheSharedScale(int nediger, int expected) =>
        Assert.Equal(expected, Assert.Single(Read($"word;{nediger}\n")).Score);

    [Theory]
    [InlineData("")]
    [InlineData("no-score-here\n")]
    [InlineData("trailing-separator;\n")]
    [InlineData("not-a-number;abc\n")]
    public void SkipsMalformedLinesRatherThanThrowing(string content) =>
        Assert.Empty(Read(content));

    [Fact]
    public void RecognisesItsOwnFormat() =>
        Assert.True(new NedigerWordListReader().CanRead(["cat;51", "dog;99"]));

    [Fact]
    public void DoesNotClaimAnEsdbFile() =>
        Assert.False(new NedigerWordListReader().CanRead(
            ["Custom wordlist generated from https://app.aspell.net/create using", "  Size: 80 (huge)"]));

    [Fact]
    public void DoesNotClaimALicenceFile() =>
        Assert.False(new NedigerWordListReader().CanRead(
            ["MIT License", "Copyright (c) 2026 bewilderingly"]));
}
