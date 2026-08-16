using Words.Core;

namespace Words.Core.Tests;

public class PersonalLexiconSourceTests
{
    private sealed class InMemoryStore(params string[] lines) : IPersonalWordStore
    {
        public List<string> Added { get; } = [];

        public ValueTask<IReadOnlyList<string>> ReadLinesAsync(CancellationToken cancellationToken = default) =>
            new((IReadOnlyList<string>)lines);

        public ValueTask AddAsync(string displayForm, CancellationToken cancellationToken = default)
        {
            Added.Add(displayForm);
            return ValueTask.CompletedTask;
        }
    }

    private static ValueTask<IReadOnlyList<Entry>> LoadAsync(params string[] lines) =>
        new PersonalLexiconSource(new InMemoryStore(lines)).LoadAsync();

    [Fact]
    public async Task ReadsPlainEntries()
    {
        var entries = await LoadAsync("bletchley", "Red Rum");

        Assert.Equal(["bletchley", "Red Rum"], entries.Select(e => e.DisplayForm).Order());
        Assert.All(entries, e => Assert.Equal(Sources.Personal, e.Sources));
    }

    [Fact]
    public async Task GivesEntriesWithoutAScoreTheDefault() =>
        Assert.Equal(PersonalLexiconSource.DefaultScore, Assert.Single(await LoadAsync("bletchley")).Score);

    [Fact]
    public async Task HonoursAnExplicitScore() =>
        Assert.Equal(30, Assert.Single(await LoadAsync("obscurity;30")).Score);

    [Fact]
    public async Task ClampsScoresToTheScale() =>
        Assert.Equal(100, Assert.Single(await LoadAsync("shouty;5000")).Score);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("# a comment")]
    [InlineData("  # an indented comment")]
    [InlineData("-'-")]
    public async Task IgnoresBlankLinesCommentsAndEntriesWithNoLetters(string line) =>
        Assert.Empty(await LoadAsync(line));

    [Fact]
    public async Task DeduplicatesWithinTheFileSoTheLastLineWins()
    {
        // A hand-edited file accumulates repeats; a source owes its consumer a clean list.
        var entry = Assert.Single(await LoadAsync("bletchley;20", "bletchley;80"));

        Assert.Equal(80, entry.Score);
    }

    [Fact]
    public async Task TreatsTheFileAsOptional() =>
        Assert.Empty(await LoadAsync());
}
