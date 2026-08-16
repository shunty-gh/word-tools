using System.Text.RegularExpressions;

namespace Words.Cli;

internal static partial class HelpText
{
    /// <summary>
    /// Moves quotes from inside a help placeholder to outside it: <c>&lt;"pattern"&gt;</c>
    /// becomes <c>"&lt;pattern&gt;"</c>.
    /// </summary>
    /// <remarks>
    /// System.CommandLine always wraps an argument's name in angle brackets, so the only way
    /// to get quotes into the usage line is to put them in the name — which then renders as
    /// though the quotes were part of the placeholder. They are not: the quotes are literal
    /// characters the user types, and the brackets mark the part they replace, so
    /// <c>"&lt;pattern&gt;"</c> is the correct notation.
    /// <para>
    /// Written as a general rule rather than for one argument, because every argument whose
    /// value contains <c>?</c> needs quoting for the same reason — <c>words anagram</c>
    /// included. If an argument is renamed and this stops matching, the help simply reverts
    /// to the framework's notation; nothing breaks.
    /// </para>
    /// </remarks>
    public static string WithQuotesOutsidePlaceholders(string help) =>
        QuotedPlaceholder().Replace(help, "\"<$1>\"");

    /// <summary>
    /// Examples and notes shown below a command's own help, or null if it has none.
    /// </summary>
    /// <remarks>
    /// Kept out of the command's description because System.CommandLine uses that for the
    /// parent's command list too, where several paragraphs each would bury the summaries.
    /// </remarks>
    public static string? ExtendedHelpFor(string commandName) => commandName switch
    {
        "pattern" => PatternCommand.ExtendedHelp,
        "anagram" => AnagramCommand.ExtendedHelp,
        _ => null,
    };

    [GeneratedRegex("""<"([^"<>]*)">""")]
    private static partial Regex QuotedPlaceholder();
}
