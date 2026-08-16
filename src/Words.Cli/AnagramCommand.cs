using System.CommandLine;
using Words.Core;

namespace Words.Cli;

/// <summary>
/// <c>words anagram</c> — answers that use exactly these letters.
/// </summary>
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
          words anagram listen --json       answers as JSON

        Letters using '?' must be quoted. Use '.' instead and you can normally do
        without. If in doubt, quote it — single or double.

        Every letter given is used, and so is every blank, so an answer is always as
        long as the letters plus the blanks. At most 3 blanks, or 1 when composing.

        Composed answers are built from ordinary single words, never from phrases or
        proper nouns, and are capped at 200 unless --limit says otherwise.
        """;

    /// <summary>
    /// Composition can produce thousands of answers, so it caps by default. The cap keeps
    /// the most likely — see <see cref="Results.Arrange"/>.
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

        // See PatternCommand: absorbs the filenames a shell substitutes for an unquoted
        // argument, so the command can explain itself instead of failing obscurely.
        var expanded = new Argument<string[]>("expanded")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Hidden = true,
        };

        var compose = new Option<bool>("--compose", "-c")
        {
            Description = "Also build answers out of two or more separate words.",
        };

        var components = new Option<int>("--components")
        {
            Description = "How many words a composed answer may use.",
            DefaultValueFactory = _ => 2,
            HelpName = "2|3",
        };

        var minLength = new Option<int>("--min-length")
        {
            Description = "The shortest word a composed answer may use.",
            DefaultValueFactory = _ => 3,
            HelpName = "n",
        };

        var options = new QueryOptions();

        var command = new Command("anagram", Summary)
        {
            letters,
            expanded,
            compose,
            components,
            minLength,
        };

        options.AddTo(command);
        command.Aliases.Add("anag");

        command.SetAction((parseResult, cancellationToken) =>
        {
            var composing = parseResult.GetValue(compose);

            return RunAsync(
                parseResult.GetValue(letters) ?? string.Empty,
                parseResult.GetValue(expanded) ?? [],
                composing
                    ? new CompositionOptions
                    {
                        MaxComponents = parseResult.GetValue(components),
                        MinComponentLength = parseResult.GetValue(minLength),
                    }
                    : null,
                options.Read(parseResult, defaultLimit: composing ? ComposeLimit : int.MaxValue),
                cancellationToken);
        });

        return command;
    }

    private static async Task<int> RunAsync(
        string letters,
        string[] expanded,
        CompositionOptions? compose,
        QuerySettings settings,
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
                new AnagramQuery { Letters = letters, Filter = settings.Filter, Compose = compose },
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

        return await QueryRunner.RunAsync(matches, settings, cancellationToken).ConfigureAwait(false);
    }
}
