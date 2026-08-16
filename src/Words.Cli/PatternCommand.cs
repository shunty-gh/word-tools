using System.CommandLine;
using Words.Core;

namespace Words.Cli;

/// <summary>
/// <c>words pattern</c> — answers that fit a shape of letters and gaps.
/// </summary>
internal static class PatternCommand
{
    /// <summary>
    /// One line, because System.CommandLine shows a command's description both in its own
    /// help and in the parent's command list. Anything longer swamps <c>words --help</c>.
    /// </summary>
    private const string Summary = "Find answers matching a crossword pattern.";

    /// <summary>Appended to this command's own help, below the options.</summary>
    public const string ExtendedHelp = """
        Examples:
          words pattern A..D          '.' is one unknown letter, and needs no quotes
          words pattern RED.ERRING    grids have no spaces, so this finds "red herring"
          words pattern "A??D"        '?' means the same as '.', but must be quoted
          words pattern "C[aeiou]T"   C, then a vowel, then T — quote it too
          words pattern A..D --json   answers as JSON

        Patterns using '?' or '[abc]' must be quoted. Use '.' instead and you can
        normally do without. If in doubt, quote it — single or double.

        A pattern's length fixes the answer's length exactly, and '?' and '.' both mean
        exactly one letter.
        """;

    public static Command Create()
    {
        // The quotes are part of the displayed name so that both the usage line and the
        // argument list show them. Setting HelpName alone reaches the argument list but
        // not the usage synopsis, which then contradicts it.
        var pattern = new Argument<string>("\"pattern\"")
        {
            Description =
                "The letters you have and the gaps you don't: 'A..D' is four letters starting with A. "
                + "Use [abc] or [^abc] to choose between letters.",
        };

        // Absorbs anything extra so a shell-expanded pattern reaches the action and can be
        // explained, rather than producing System.CommandLine's "unrecognized argument".
        // Hidden, so the usage line stays honest about accepting exactly one pattern.
        var expanded = new Argument<string[]>("expanded")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Hidden = true,
        };

        var options = new QueryOptions();

        var command = new Command("pattern", Summary)
        {
            pattern,
            expanded,
        };

        options.AddTo(command);
        command.Aliases.Add("pat");

        command.SetAction((parseResult, cancellationToken) => RunAsync(
            parseResult.GetValue(pattern) ?? string.Empty,
            parseResult.GetValue(expanded) ?? [],
            // A pattern fixes the answer's length, so results are naturally few: no cap.
            options.Read(parseResult, defaultLimit: int.MaxValue),
            cancellationToken));

        return command;
    }

    private static async Task<int> RunAsync(
        string pattern,
        string[] expanded,
        QuerySettings settings,
        CancellationToken cancellationToken)
    {
        if (expanded.Length > 0)
        {
            // '?' and '[abc]' are glob characters. These arguments are the filenames the
            // shell substituted for the pattern before we ever saw it — so we cannot know
            // what was actually typed, and must not pretend to by quoting one back.
            var substituted = string.Join(", ", new[] { pattern }.Concat(expanded).Take(4));
            var ellipsis = expanded.Length + 1 > 4 ? ", …" : string.Empty;

            Console.Error.WriteLine("words: the pattern must be quoted.");
            Console.Error.WriteLine(
                $"       '?' and '[abc]' are shell wildcards, so your shell replaced it with "
                + $"{expanded.Length + 1} matching filenames before words ran:");
            Console.Error.WriteLine($"       {substituted}{ellipsis}");
            Console.Error.WriteLine(
                "       Quote it, or use '.' instead of '?' — '.' is not a wildcard, so it needs no quotes:");
            Console.Error.WriteLine("         words pattern \"C?T\"    or    words pattern C.T");
            return ExitCodes.BadRequest;
        }

        var lexicon = await Composition.LoadLexiconAsync(cancellationToken).ConfigureAwait(false);
        var engine = new WordEngine(lexicon);

        IAsyncEnumerable<Match> matches;

        try
        {
            matches = engine.QueryAsync(
                new PatternQuery { Pattern = pattern, Filter = settings.Filter },
                cancellationToken);
        }
        catch (QuerySyntaxException error)
        {
            Console.Error.WriteLine($"words: {error.ToDiagnostic()}");
            return ExitCodes.BadRequest;
        }

        return await QueryRunner.RunAsync(matches, settings, cancellationToken).ConfigureAwait(false);
    }
}
