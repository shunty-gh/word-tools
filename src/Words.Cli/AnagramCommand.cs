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
          words anagram listen              every rearrangement of these letters
          words anagram trisec.             '.' is a letter you do not know yet
          words anagram "trisec?"           '?' means the same, but must be quoted
          words anagram catdog --compose    answers built from separate words

        Letters using '?' must be quoted. Use '.' instead and you can normally do
        without. If in doubt, quote it — single or double.

        Every letter given is used, and so is every blank, so an answer is always as
        long as the letters plus the blanks. At most 3 blanks, or 1 when composing.

        Composed answers are built from ordinary single words, never from phrases or
        proper nouns. Only the 200 most likely are shown.
        """;

    /// <summary>
    /// How many composed answers to show. Composition can produce thousands, so the most
    /// likely are kept — see the ranking in <see cref="RunAsync"/>. `--limit` arrives in
    /// phase 6.
    /// </summary>
    private const int ComposeLimit = 200;

    public static Command Create()
    {
        var letters = new Argument<string>("\"letters\"")
        {
            Description =
                "The letters you have, with '?' or '.' for each one you do not know. "
                + "Spaces, hyphens, apostrophes and accents are all forgiven.",
        };

        var compose = new Option<bool>("--compose", "-c")
        {
            Description = "Also build answers out of two or more separate words.",
        };

        var components = new Option<int>("--components")
        {
            Description = "How many words a composed answer may use, 2 or 3.",
            DefaultValueFactory = _ => 2,
        };

        var minLength = new Option<int>("--min-length")
        {
            Description = "The shortest word a composed answer may use.",
            DefaultValueFactory = _ => 3,
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
            compose,
            components,
            minLength,
        };

        command.Aliases.Add("anag");

        command.SetAction((parseResult, cancellationToken) => RunAsync(
            parseResult.GetValue(letters) ?? string.Empty,
            parseResult.GetValue(expanded) ?? [],
            parseResult.GetValue(compose)
                ? new CompositionOptions
                {
                    MaxComponents = parseResult.GetValue(components),
                    MinComponentLength = parseResult.GetValue(minLength),
                }
                : null,
            cancellationToken));

        return command;
    }

    private static async Task<int> RunAsync(
        string letters,
        string[] expanded,
        CompositionOptions? compose,
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
            return ExitCodes.BadRequest;
        }

        var lexicon = await Composition.LoadLexiconAsync(cancellationToken).ConfigureAwait(false);
        var engine = new WordEngine(lexicon);

        IAsyncEnumerable<Match> matches;

        try
        {
            matches = engine.QueryAsync(
                new AnagramQuery { Letters = letters, Compose = compose },
                cancellationToken);
        }
        catch (QuerySyntaxException error)
        {
            Console.Error.WriteLine($"words: {error.ToDiagnostic()}");
            return ExitCodes.BadRequest;
        }
        catch (ArgumentOutOfRangeException error)
        {
            Console.Error.WriteLine($"words: {error.Message.Split(" (Parameter")[0]}");
            return ExitCodes.BadRequest;
        }

        var found = new List<Match>();

        try
        {
            await foreach (var match in matches.ConfigureAwait(false))
            {
                found.Add(match);
            }
        }
        catch (OperationCanceledException)
        {
            // Ctrl-C. Say nothing — the user knows — but do not report the partial search
            // as though it had found nothing.
            return ExitCodes.Interrupted;
        }

        var total = found.Count;

        // Composition can produce thousands of answers, so the cap has to select
        // meaningfully rather than alphabetically: rank by fewest words, then by the
        // weakest word in the answer, and keep the best.
        if (compose is not null && total > ComposeLimit)
        {
            found = [.. found
                .OrderBy(m => m.Components.Count)
                .ThenByDescending(m => m.Score)
                .ThenBy(m => m.DisplayForm, StringComparer.OrdinalIgnoreCase)
                .Take(ComposeLimit)];
        }

        // Alphabetical is the default ordering; `--sort` arrives in phase 6.
        found.Sort((left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.DisplayForm, right.DisplayForm));

        foreach (var match in found)
        {
            Console.WriteLine(match.DisplayForm);
        }

        if (found.Count < total)
        {
            // Stderr, so a pipe to wc or grep sees only answers.
            Console.Error.WriteLine(
                $"words: showing the {found.Count:N0} most likely of {total:N0} answers.");
        }

        return total > 0 ? ExitCodes.Found : ExitCodes.NothingFound;
    }
}
