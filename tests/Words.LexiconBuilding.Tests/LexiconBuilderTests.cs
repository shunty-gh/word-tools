using Words.Core;
using Words.LexiconBuilding;

namespace Words.LexiconBuilding.Tests;

public sealed class LexiconBuilderTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("words-lexicon-tests").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private void WriteEsdb(string name, int size, params string[] entries) =>
        File.WriteAllText(
            Path.Combine(_directory, name),
            $"""
             Custom wordlist generated from https://app.aspell.net/create using
             the English Speller Database (ESDB) with parameters:
               Size: {size} (huge)

             {string.Join('\n', entries)}

             """);

    private void WriteNediger(string name, params string[] scoredEntries) =>
        File.WriteAllText(Path.Combine(_directory, name), string.Join("\r\n", scoredEntries) + "\r\n");

    private LexiconBuildResult Build() => LexiconBuilder.Build(_directory);

    [Fact]
    public void MergesIdenticalDisplayFormsCombiningProvenance()
    {
        WriteEsdb("esdb-size80.txt", 80, "cat");
        WriteNediger("nediger.txt", "cat;99");

        var entry = Assert.Single(Build().Entries);

        Assert.Equal(Sources.Esdb | Sources.Nediger, entry.Sources);
    }

    [Fact]
    public void TakesTheMostGenerousScoreAcrossSources()
    {
        WriteEsdb("esdb-size80.txt", 80, "cat");     // scores 50
        WriteNediger("nediger.txt", "cat;99");        // scores 90

        Assert.Equal(90, Assert.Single(Build().Entries).Score);
    }

    [Fact]
    public void TakesTheSmallestBandAnEntryAppearsInAcrossCumulativeEsdbLists()
    {
        // The size bands are cumulative supersets, so a common word appears in every one.
        WriteEsdb("esdb-size35.txt", 35, "cat");
        WriteEsdb("esdb-size80.txt", 80, "cat", "aardwolf");

        var entries = Build().Entries.ToDictionary(e => e.DisplayForm, StringComparer.Ordinal);

        Assert.Equal(100, entries["cat"].Score);
        Assert.Equal(50, entries["aardwolf"].Score);
    }

    [Fact]
    public void KeepsDistinctDisplayFormsThatShareASearchKey()
    {
        // Polish and polish are genuinely different answers; collapsing them loses one.
        WriteEsdb("esdb-size80.txt", 80, "Polish", "polish");

        var entries = Build().Entries;

        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Equal("POLISH", e.SearchKey));
    }

    [Fact]
    public void StaysRacyIfAnySourceSaysSo()
    {
        WriteEsdb("esdb-size80.txt", 80, "rude");
        WriteNediger("nediger.txt", "rude;49");

        Assert.True(Assert.Single(Build().Entries).IsRacy);
    }

    [Fact]
    public void SkipsFilesNoReaderRecognises()
    {
        WriteNediger("nediger.txt", "cat;51");
        File.WriteAllText(Path.Combine(_directory, "LICENSE.txt"), "MIT License\n\nCopyright (c) 2026\n");

        var result = Build();

        Assert.Single(result.Entries);
        Assert.Single(result.Manifest.Sources);
    }

    [Fact]
    public void DiscardsEntriesWithNoUsableLetters()
    {
        WriteNediger("nediger.txt", "cat;51", "-'-;51");

        var result = Build();

        Assert.Single(result.Entries);
        Assert.Equal(1, result.Manifest.DiscardedCount);
    }

    [Fact]
    public void OrdersEntriesDeterministicallySoRebuildsDoNotChurn()
    {
        WriteNediger("nediger.txt", "zebra;51", "cat;51", "Red Herring;51");

        Assert.Equal(
            Build().Entries.Select(e => e.DisplayForm),
            Build().Entries.Select(e => e.DisplayForm));
    }

    [Fact]
    public void RecordsEachSourceInTheManifest()
    {
        WriteEsdb("esdb-size80.txt", 80, "cat");
        WriteNediger("nediger.txt", "dog;51");

        var manifest = Build().Manifest;

        Assert.Equal(2, manifest.Sources.Count);
        Assert.Contains(manifest.Sources, s => s.Reader == "ESDB");
        Assert.Contains(manifest.Sources, s => s.Reader == "Nediger");
        Assert.All(manifest.Sources, s => Assert.Equal(64, s.Sha256.Length));
    }
}
