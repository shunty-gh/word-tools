using Words.Cli;
using Words.Core;

namespace Words.Cli.Tests;

public class ResultsTests
{
    private const int NoLimit = int.MaxValue;

    private static Match Single(string displayForm, int score) =>
        Match.Of(Entry.Create(displayForm, score, Sources.Esdb));

    private static Match Composed(params (string Word, int Score)[] words) =>
        new([.. words.Select(w => Entry.Create(w.Word, w.Score, Sources.Esdb))]);

    private static string[] Arrange(IReadOnlyList<Match> matches, SortOrder sort, int limit) =>
        [.. Results.Arrange(matches, sort, limit).Select(m => m.DisplayForm)];

    [Fact]
    public void SortsAlphabeticallyByDefault()
    {
        var matches = new[] { Single("zebra", 10), Single("apple", 90), Single("mango", 50) };

        Assert.Equal(["apple", "mango", "zebra"], Arrange(matches, SortOrder.Alpha, NoLimit));
    }

    [Fact]
    public void AlphabeticalOrderIgnoresCase()
    {
        var matches = new[] { Single("Zebra", 10), Single("apple", 10) };

        Assert.Equal(["apple", "Zebra"], Arrange(matches, SortOrder.Alpha, NoLimit));
    }

    [Fact]
    public void SortsByScoreHighestFirst()
    {
        var matches = new[] { Single("rare", 10), Single("common", 90), Single("middling", 50) };

        Assert.Equal(["common", "middling", "rare"], Arrange(matches, SortOrder.Score, NoLimit));
    }

    [Fact]
    public void SortsByLengthShortestFirst()
    {
        var matches = new[] { Single("elephant", 50), Single("cat", 50), Single("horse", 50) };

        Assert.Equal(["cat", "horse", "elephant"], Arrange(matches, SortOrder.Length, NoLimit));
    }

    [Fact]
    public void KeepsEverythingWhenUnderTheLimit()
    {
        var matches = new[] { Single("cat", 50), Single("dog", 50) };

        Assert.Equal(2, Arrange(matches, SortOrder.Alpha, 10).Length);
    }

    [Fact]
    public void TheLimitSelectsTheMostLikelyNotTheAlphabeticallyFirst()
    {
        // The whole point of ranking before truncating: sorting first would keep "aardvark"
        // and "abacus" and throw away the common word.
        var matches = new[]
        {
            Single("aardvark", 10),
            Single("abacus", 20),
            Single("common", 90),
        };

        Assert.Equal(["common"], Arrange(matches, SortOrder.Alpha, 1));
    }

    [Fact]
    public void SurvivorsAreThenPutIntoTheRequestedOrder()
    {
        var matches = new[]
        {
            Single("zebra", 90),
            Single("apple", 80),
            Single("mango", 10),
        };

        // Ranking keeps zebra and apple; display order is alphabetical.
        Assert.Equal(["apple", "zebra"], Arrange(matches, SortOrder.Alpha, 2));
    }

    [Fact]
    public void FewerWordsRankAheadOfMoreWords()
    {
        var matches = new[]
        {
            Composed(("aaa", 99), ("bbb", 99), ("ccc", 99)),
            Composed(("zzz", 50), ("yyy", 50)),
        };

        // Two words beat three, even though every word in the three-word answer scores
        // higher and it sorts first alphabetically.
        Assert.Equal(["zzz yyy"], Arrange(matches, SortOrder.Alpha, 1));
    }

    [Fact]
    public void ACompositionIsRankedByItsWeakestWord()
    {
        var matches = new[]
        {
            Composed(("aaa", 90), ("bbb", 10)),
            Composed(("ccc", 60), ("ddd", 60)),
        };

        Assert.Equal(["ccc ddd"], Arrange(matches, SortOrder.Alpha, 1));
    }

    [Fact]
    public void HandlesNoMatchesAtAll() =>
        Assert.Empty(Arrange([], SortOrder.Alpha, NoLimit));
}
