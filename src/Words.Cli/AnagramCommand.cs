using System.CommandLine;
using Words.Core;

namespace Words.Cli;

/// <summary>
/// <c>words anagram</c> — answers that use exactly these letters.
/// </summary>
/// <remarks>
/// Minimal for phase 4, like <see cref="PatternCommand"/>. <c>--compose</c> is phase 5;
/// <c>--json</c>, <c>--limit</c>, <c>--sort</c>, <c>--source</c> and <c>--include-racy</c>
/// are phase 6.
/// </remarks>
internal static class AnagramCommand
{
    /// <summary>See <see cref="PatternCommand"/> for why this is only one line.</summary>
    private const string Summary = "Find answers that use exactly these letters.";

    /// <summary>Appended to this command's own help, below the options.</summary>
    public const string ExtendedHelp = """
        Examples:
          words anagram listen        every rearrangement of these letters
          words anagram trisec.       '.' is a letter you do not know yet
          words anagram "trisec?"     '?' means the same, but must be quoted

        Letters using '?' must be quoted. Use '.' instead and you can normally do
        without. If in doubt, quote it — single or double.

        Every letter given is used, and so is every blank, so an answer is always as
        long as the letters plus the blanks. At most 3 blanks.
        """;

    public static Command Create()
    {
        var letters = new Argument<string>("\"letters\"")
        {
            Description =
                "The letters you have, with '?' or '.' for each one you do not know. "
                + "Spaces, hyphens, apostrophes and accents are all forgiven.",
        };

        // See PatternCommand: absorbs the filenames a shell substitutes for an unquoted
        // argument, so the command can explain itself instead of failing obscurely.
        var expanded = new Argument<string[]>("expanded")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Hidden = true,
        };

        var command = new Command("anagram", Summary)
        {
            letters,
            expanded,
        };

        command.Aliases.Add("anag");

        command.SetAction((parseResult, cancellationToken) => RunAsync(
            parseResult.GetValue(letters) ?? string.Empty,
            parseResult.GetValue(expanded) ?? [],
            cancellationToken));

        return command;
    }

    private static async Task<int> RunAsync(
        string letters,
        string[] expanded,
        CancellationToken cancellationToken)
    {
        if (expanded.Length > 0)
        {
            var substituted = string.Join(", ", new[] { letters }.Concat(expanded).Take(4));
            var ellipsis = expanded.Length + 1 > 4 ? ", …" : string.Empty;

            Console.Error.WriteLine("words: the letters must be quoted.");
            Console.Error.WriteLine(
                $"       '?' is a shell wildcard, so your shell replaced them with "
                + $"{expanded.Length + 1} matching filenames before words ran:");
            Console.Error.WriteLine($"       {substituted}{ellipsis}");
            Console.Error.WriteLine(
                "       Quote them, or use '.' instead of '?' — '.' needs no quotes:");
            Console.Error.WriteLine("         words anagram \"trisec?\"    or    words anagram trisec.");
            return 2;
        }

        var lexicon = await Composition.LoadLexiconAsync(cancellationToken).ConfigureAwait(false);
        var engine = new WordEngine(lexicon);

        IAsyncEnumerable<Match> matches;

        try
        {
            matches = engine.QueryAsync(new AnagramQuery { Letters = letters }, cancellationToken);
        }
        catch (QuerySyntaxException error)
        {
            Console.Error.WriteLine($"words: {error.ToDiagnostic()}");
            return 2;
        }

        var found = new List<string>();

        await foreach (var match in matches.ConfigureAwait(false))
        {
            found.Add(match.DisplayForm);
        }

        // Alphabetical is the default ordering; `--sort` arrives in phase 6.
        found.Sort(StringComparer.OrdinalIgnoreCase);

        foreach (var displayForm in found)
        {
            Console.WriteLine(displayForm);
        }

        return found.Count > 0 ? 0 : 1;
    }
}
