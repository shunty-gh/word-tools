namespace Words.Core;

/// <summary>
/// A pattern that could not be understood, and where it went wrong.
/// </summary>
public sealed class PatternSyntaxException(string pattern, int position, string message)
    : Exception(message)
{
    /// <summary>The pattern as supplied.</summary>
    public string Pattern { get; } = pattern;

    /// <summary>
    /// Where the problem is, counting from 1 so it reads as a column number to a person.
    /// </summary>
    public int Position { get; } = position;

    /// <summary>
    /// The message with the offending position marked underneath, for showing in a
    /// terminal. Saying "position 4" is accurate; pointing at it is useful.
    /// </summary>
    public string ToDiagnostic() =>
        $"{Message}{Environment.NewLine}  {Pattern}{Environment.NewLine}  {new string(' ', Math.Max(0, Position - 1))}^";
}
