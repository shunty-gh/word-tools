using Words.Core;

namespace Words.Core.Tests;

public class AnagramLettersTests
{
    [Fact]
    public void SortsTheLettersItWasGiven() =>
        Assert.Equal("ACT", AnagramLetters.Parse("cat").Letters);

    [Fact]
    public void FoldsCase() =>
        Assert.Equal("ACT", AnagramLetters.Parse("CaT").Letters);

    [Theory]
    [InlineData("naïve", "AEINV")]
    [InlineData("café", "ACEF")]
    public void FoldsAccentsToTheirBaseLetter(string input, string expected) =>
        Assert.Equal(expected, AnagramLetters.Parse(input).Letters);

    [Theory]
    [InlineData("red herring")]
    [InlineData("red-herring")]
    [InlineData("red herr'ing")]
    [InlineData("redherring")]
    public void IgnoresSpacesHyphensAndApostrophes(string input) =>
        Assert.Equal("DEEGHINRRR", AnagramLetters.Parse(input).Letters);

    [Theory]
    [InlineData("cat?", 1)]
    [InlineData("cat.", 1)]
    [InlineData("c?t.", 2)]
    [InlineData("???", 3)]
    public void CountsBothSpellingsOfABlank(string input, int expected) =>
        Assert.Equal(expected, AnagramLetters.Parse(input).Blanks);

    [Fact]
    public void AnEllipsisCountsAsThreeBlanks()
    {
        // Apple platforms replace "..." with a single character as it is typed.
        var letters = AnagramLetters.Parse("cat…");

        Assert.Equal(3, letters.Blanks);
        Assert.Equal(6, letters.Length);
    }

    [Fact]
    public void AnEllipsisCanExceedTheBlankLimit()
    {
        // Three dots is already the maximum, so a letter plus an ellipsis is one too many.
        var error = Error("cat.…");

        Assert.Contains("At most 3", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LengthIsLettersPlusBlanks()
    {
        var letters = AnagramLetters.Parse("trisec?");

        Assert.Equal(6, letters.Letters.Length);
        Assert.Equal(1, letters.Blanks);
        Assert.Equal(7, letters.Length);
    }

    [Fact]
    public void AcceptsBlanksWithNoLetters() =>
        Assert.Equal(3, AnagramLetters.Parse("???").Length);

    // -- errors --

    private static QuerySyntaxException Error(string input) =>
        Assert.Throws<QuerySyntaxException>(() => AnagramLetters.Parse(input));

    [Fact]
    public void RejectsMoreBlanksThanAllowed()
    {
        var error = Error("cat????");

        Assert.Equal(7, error.Position);
        Assert.Contains("At most 3", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cat*", 4)]
    [InlineData("ca8t", 3)]
    [InlineData("[cat]", 1)]
    public void RejectsCharactersThatAreNeitherLettersNorBlanks(string input, int position) =>
        Assert.Equal(position, Error(input).Position);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("--")]
    public void RejectsInputWithNothingInIt(string input) =>
        Assert.Equal(1, Error(input).Position);

    // -- canonical forms --

    [Fact]
    public void WithNoBlanksThereIsOneCanonicalForm() =>
        Assert.Equal(["ACT"], AnagramLetters.Parse("cat").CanonicalForms());

    [Theory]
    [InlineData(1, 26)]
    [InlineData(2, 351)]
    [InlineData(3, 3276)]
    public void BlanksProduceCombinationsWithRepetition(int blanks, int expected)
    {
        // 26 letters choose k with repetition: C(26+k-1, k).
        var letters = AnagramLetters.Parse("cat" + new string('?', blanks));

        Assert.Equal(expected, letters.CanonicalForms().Count());
    }

    [Fact]
    public void EveryCanonicalFormIsDistinctSoNoAnswerCanBeFoundTwice()
    {
        var forms = AnagramLetters.Parse("cat??").CanonicalForms().ToList();

        Assert.Equal(forms.Count, forms.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryCanonicalFormIsSortedAndTheRightLength()
    {
        var letters = AnagramLetters.Parse("cat??");

        Assert.All(letters.CanonicalForms(), form =>
        {
            Assert.Equal(letters.Length, form.Length);
            Assert.Equal(new string([.. form.Order()]), form);
        });
    }

    [Fact]
    public void CanonicalFormsAlwaysContainTheKnownLetters()
    {
        // Whatever the blanks turn out to be, the letters given are always used.
        Assert.All(
            AnagramLetters.Parse("cat?").CanonicalForms(),
            form => Assert.All("ACT", letter => Assert.Contains(letter, form)));
    }
}
