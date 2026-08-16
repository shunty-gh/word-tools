namespace Words.Core;

/// <summary>
/// A query the engine could not understand, and where it went wrong.
/// </summary>
/// <remarks>
/// Shared by both query kinds so that a malformed pattern and malformed anagram letters
/// are reported the same way, and the CLI needs only one way of showing them.
/// </remarks>
public sealed class QuerySyntaxException(string input, int position, string message)
    : Exception(message)
{
    /// <summary>The pattern or letters as supplied.</summary>
    public string Input { get; } = input;

    /// <summary>
    /// Where the problem is, counting from 1 so it reads as a column number to a person.
    /// </summary>
    public int Position { get; } = position;

    /// <summary>
    /// The message with the offending position marked underneath, for showing in a
    /// terminal. Saying "position 4" is accurate; pointing at it is useful.
    /// </summary>
    public string ToDiagnostic() =>
        $"{Message}{Environment.NewLine}  {Input}{Environment.NewLine}  {new string(' ', Math.Max(0, Position - 1))}^";
}
