using Words.Core;

namespace Words.Core.Tests;

public class EntryTests
{
    [Theory]
    [InlineData("cat")]
    [InlineData("well-known")]
    [InlineData("cat's")]
    public void EntriesWithoutSpacesAreSingleWords(string displayForm)
    {
        var entry = Entry.Create(displayForm, 50, Sources.Esdb);

        Assert.True(entry.Kinds.HasFlag(EntryKinds.SingleWord));
        Assert.False(entry.Kinds.HasFlag(EntryKinds.Phrase));
    }

    [Fact]
    public void EntriesWithSpacesArePhrases()
    {
        var entry = Entry.Create("red herring", 50, Sources.Nediger);

        Assert.True(entry.Kinds.HasFlag(EntryKinds.Phrase));
        Assert.False(entry.Kinds.HasFlag(EntryKinds.SingleWord));
    }

    [Fact]
    public void CapitalisedEntriesAreProperNouns() =>
        Assert.True(Entry.Create("Paris", 50, Sources.Esdb).Kinds.HasFlag(EntryKinds.ProperNoun));

    [Fact]
    public void LowercaseEntriesAreNotProperNouns() =>
        Assert.False(Entry.Create("paris", 50, Sources.Esdb).Kinds.HasFlag(EntryKinds.ProperNoun));

    [Fact]
    public void LeadingPunctuationDoesNotHideACapital() =>
        Assert.True(Entry.Create("'Allo 'Allo", 50, Sources.Nediger).Kinds.HasFlag(EntryKinds.ProperNoun));

    [Fact]
    public void PhrasesCanAlsoBeProperNouns()
    {
        var entry = Entry.Create("Red Rum", 50, Sources.Nediger);

        Assert.True(entry.Kinds.HasFlag(EntryKinds.Phrase));
        Assert.True(entry.Kinds.HasFlag(EntryKinds.ProperNoun));
    }

    [Fact]
    public void CreateDerivesTheSearchKeyFromTheDisplayForm()
    {
        var entry = Entry.Create("Red Herring", 50, Sources.Nediger);

        Assert.Equal("Red Herring", entry.DisplayForm);
        Assert.Equal("REDHERRING", entry.SearchKey);
    }
}
