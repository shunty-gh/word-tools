using Words.Core;

namespace Words.Core.Tests;

public class PatternMatcherTests
{
    private static bool Matches(string pattern, string searchKey) =>
        PatternMatcher.Compile(pattern).Matches(searchKey);

    [Theory]
    [InlineData("CAT", "CAT")]
    [InlineData("cat", "CAT")]
    [InlineData("cAt", "CAT")]
    public void LiteralsMatchCaseInsensitively(string pattern, string key) =>
        Assert.True(Matches(pattern, key));

    [Fact]
    public void LiteralsMustMatchExactly() => Assert.False(Matches("CAT", "COT"));

    [Theory]
    [InlineData("?AT", "CAT")]
    [InlineData("C?T", "CAT")]
    [InlineData("???", "CAT")]
    public void QuestionMarkMatchesExactlyOneLetter(string pattern, string key) =>
        Assert.True(Matches(pattern, key));

    [Theory]
    [InlineData("??", "CAT")]
    [InlineData("????", "CAT")]
    public void PatternLengthFixesAnswerLength(string pattern, string key) =>
        Assert.False(Matches(pattern, key));

    [Theory]
    [InlineData(".AT", "CAT")]
    [InlineData("C.T", "CAT")]
    [InlineData("...", "CAT")]
    public void FullStopAlsoMatchesExactlyOneLetter(string pattern, string key) =>
        Assert.True(Matches(pattern, key));

    [Fact]
    public void FullStopAndQuestionMarkAreInterchangeable()
    {
        // '.' exists because it is not a shell wildcard, so letters-and-dots patterns can
        // be typed without quotes. It means exactly what '?' means.
        Assert.True(Matches("C.T", "CAT"));
        Assert.True(Matches("C?T", "CAT"));
        Assert.True(Matches("A.?D", "ABCD"));
        Assert.Equal(PatternMatcher.Compile("A..D").Length, PatternMatcher.Compile("A??D").Length);
    }

    [Fact]
    public void FullStopIsStillNotRegex()
    {
        // '.' happens to agree with its regex meaning, but the language around it does not:
        // there is no '*', so '.*' is rejected rather than treated as "any run".
        Assert.Contains("not allowed", Error("A.*").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FullStopIsNotAllowedInsideAClass() =>
        Assert.Equal(4, Error("A[b.c]").Position);

    [Fact]
    public void QuestionMarkIsNotTheRegexQuestionMark()
    {
        // As a regular expression, `colou?r` matches both COLOUR and COLOR, because there
        // `?` makes the preceding `u` optional. Here `?` consumes a letter of its own, so
        // the pattern is seven letters long and matches neither six-letter spelling.
        Assert.False(Matches("COLOU?R", "COLOUR"));
        Assert.False(Matches("COLOU?R", "COLOR"));

        // It matches seven letters with anything in the sixth position.
        Assert.True(Matches("COLOU?R", "COLOUER"));
    }

    [Fact]
    public void LengthCountsElementsNotCharacters()
    {
        // "[bc]" is four characters but one element, so this pattern is three letters long.
        var matcher = PatternMatcher.Compile("A[bc]T");

        Assert.Equal(3, matcher.Length);
        Assert.True(matcher.Matches("ABT"));
    }

    [Theory]
    [InlineData("C[aeiou]T", "CAT", true)]
    [InlineData("C[aeiou]T", "COT", true)]
    [InlineData("C[aeiou]T", "CST", false)]
    public void ClassesAdmitOnlyTheirLetters(string pattern, string key, bool expected) =>
        Assert.Equal(expected, Matches(pattern, key));

    [Theory]
    [InlineData("C[^aeiou]T", "CST", true)]
    [InlineData("C[^aeiou]T", "CAT", false)]
    public void NegatedClassesAdmitEverythingElse(string pattern, string key, bool expected) =>
        Assert.Equal(expected, Matches(pattern, key));

    [Fact]
    public void ClassesAreCaseInsensitive() => Assert.True(Matches("C[AEIOU]T", "CAT"));

    [Fact]
    public void RepeatedLettersInAClassAreHarmless() => Assert.True(Matches("C[aaa]T", "CAT"));

    [Fact]
    public void ElementsCombineFreely()
    {
        var matcher = PatternMatcher.Compile("?[aeiou]?[^z]");

        Assert.Equal(4, matcher.Length);
        Assert.True(matcher.Matches("BALD"));
        Assert.False(matcher.Matches("BXLD"));
    }

    // -- syntax errors. Positions count from 1, so they read as column numbers. --

    private static PatternSyntaxException Error(string pattern) =>
        Assert.Throws<PatternSyntaxException>(() => PatternMatcher.Compile(pattern));

    [Fact]
    public void RejectsAnEmptyPattern() => Assert.Equal(1, Error("").Position);

    [Theory]
    [InlineData("A*D", 2)]
    [InlineData("A D", 2)]
    [InlineData("*AD", 1)]
    [InlineData("AD-", 3)]
    [InlineData("AB3", 3)]
    public void RejectsCharactersThatAreNotPartOfTheLanguage(string pattern, int position)
    {
        var error = Error(pattern);

        Assert.Equal(position, error.Position);
        Assert.Contains("not allowed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnAsteriskExplicitly()
    {
        // There is deliberately no variable-length construct: it would contradict the rule
        // that pattern length fixes answer length.
        Assert.Contains("not allowed", Error("A*").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnUnclosedClass()
    {
        var error = Error("A[bc");

        Assert.Equal(2, error.Position);
        Assert.Contains("no matching ']'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAClosingBracketWithoutAnOpeningOne()
    {
        var error = Error("A]b");

        Assert.Equal(2, error.Position);
        Assert.Contains("no matching '['", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("A[]")]
    [InlineData("A[^]")]
    public void RejectsAClassListingNoLetters(string pattern) =>
        Assert.Equal(2, Error(pattern).Position);

    [Fact]
    public void RejectsNonLettersInsideAClass()
    {
        var error = Error("A[b1c]");

        Assert.Equal(4, error.Position);
        Assert.Contains("not a letter", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PointsAtTheOffendingCharacter()
    {
        var diagnostic = Error("AB*D").ToDiagnostic();

        Assert.Contains("  AB*D", diagnostic, StringComparison.Ordinal);
        Assert.Contains("    ^", diagnostic, StringComparison.Ordinal);
    }
}
