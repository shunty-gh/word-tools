using Words.Core;

namespace Words.Core.Tests;

public class LexiconArtefactTests
{
    private static readonly Entry[] Sample =
    [
        Entry.Create("café", 80, Sources.Esdb),
        Entry.Create("Red Herring", 60, Sources.Nediger),
        Entry.Create("rude", 50, Sources.Nediger, isRacy: true),
        Entry.Create("cat", 100, Sources.Esdb | Sources.Nediger),
    ];

    private static async Task<IReadOnlyList<Entry>> RoundTripAsync(IEnumerable<Entry> entries)
    {
        using var stream = new MemoryStream();
        await LexiconArtefact.WriteAsync(stream, entries);

        stream.Position = 0;
        return await LexiconArtefact.ReadAsync(stream);
    }

    [Fact]
    public async Task RoundTripsEveryEntryUnchanged()
    {
        var read = await RoundTripAsync(Sample);

        Assert.Equal(Sample.Length, read.Count);
        Assert.Equal(Sample.OrderBy(e => e.DisplayForm, StringComparer.Ordinal), read.OrderBy(e => e.DisplayForm, StringComparer.Ordinal));
    }

    [Fact]
    public async Task PreservesDiacriticsInDisplayFormWhileKeyingWithout()
    {
        var read = await RoundTripAsync(Sample);
        var cafe = read.Single(e => e.SearchKey == "CAFE");

        Assert.Equal("café", cafe.DisplayForm);
    }

    [Fact]
    public async Task PreservesRacyFlagAndCombinedSources()
    {
        var read = await RoundTripAsync(Sample);

        Assert.True(read.Single(e => e.DisplayForm == "rude").IsRacy);
        Assert.Equal(Sources.Esdb | Sources.Nediger, read.Single(e => e.DisplayForm == "cat").Sources);
    }

    [Fact]
    public async Task RejectsAStreamThatIsNotAnArtefact()
    {
        using var stream = new MemoryStream();
        await using (var gzip = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        await using (var writer = new StreamWriter(gzip))
        {
            await writer.WriteLineAsync("not-a-lexicon");
        }

        stream.Position = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => LexiconArtefact.ReadAsync(stream));
    }

    [Fact]
    public async Task RejectsDisplayFormsContainingTheFieldSeparator()
    {
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => LexiconArtefact.WriteAsync(stream, [Entry.Create("a\tb", 50, Sources.Personal)]));
    }
}
