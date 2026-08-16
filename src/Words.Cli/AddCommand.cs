using System.CommandLine;
using Words.Core;

namespace Words.Cli;

/// <summary>
/// <c>words add</c> — puts a word or phrase into the personal list, which is merged into
/// the lexicon on every query.
/// </summary>
internal static class AddCommand
{
    private const string Summary = "Add a word or phrase to your personal list.";

    public const string ExtendedHelp = """
        Examples:
          words add "bletchley park"        add a phrase
          words add jabberwock --score 40   add it, but rank it low

        The list is a plain text file you can edit by hand, one entry per line, with an
        optional ";score" after each. Lines starting with # are ignored.
        """;

    public static Command Create()
    {
        var entry = new Argument<string>("entry")
        {
            Description = "The word or phrase to add. Quote it if it contains spaces.",
        };

        var score = new Option<int>("--score")
        {
            Description = "How highly to rank it, 0 to 100.",
            DefaultValueFactory = _ => PersonalLexiconSource.DefaultScore,
        };

        var command = new Command("add", Summary) { entry };
        command.Options.Add(score);

        command.SetAction((parseResult, cancellationToken) => RunAsync(
            parseResult.GetValue(entry) ?? string.Empty,
            parseResult.GetValue(score),
            cancellationToken));

        return command;
    }

    private static async Task<int> RunAsync(string entry, int score, CancellationToken cancellationToken)
    {
        var displayForm = entry.Trim();

        if (SearchKeys.From(displayForm).Length == 0)
        {
            Console.Error.WriteLine($"words: '{entry}' has no letters, so it could never be an answer.");
            return ExitCodes.BadRequest;
        }

        if (score is < 0 or > 100)
        {
            Console.Error.WriteLine($"words: a score must be between 0 and 100, not {score}.");
            return ExitCodes.BadRequest;
        }

        var store = new FilePersonalWordStore(Composition.PersonalWordsPath);

        // The score is only written when it differs from the default, so a hand-edited file
        // stays as readable as possible.
        var line = score == PersonalLexiconSource.DefaultScore
            ? displayForm
            : $"{displayForm};{score}";

        await store.AddAsync(line, cancellationToken).ConfigureAwait(false);

        Console.Error.WriteLine($"words: added '{displayForm}' to {store.Path}");
        return ExitCodes.Found;
    }
}
