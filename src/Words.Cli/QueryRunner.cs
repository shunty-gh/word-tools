using Words.Core;

namespace Words.Cli;

/// <summary>
/// Collects a query's answers and writes them out. Shared so both query commands present
/// results the same way.
/// </summary>
internal static class QueryRunner
{
    public static async Task<int> RunAsync(
        IAsyncEnumerable<Match> matches,
        QuerySettings settings,
        CancellationToken cancellationToken)
    {
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
            // Ctrl-C. Say nothing — the user knows — but do not report the abandoned search
            // as though it had found nothing.
            return ExitCodes.Interrupted;
        }

        var total = found.Count;
        var shown = MatchOrdering.Arrange(found, settings.Sort, settings.Limit);

        if (settings.Json)
        {
            // The payload carries the counts, so no separate truncation notice.
            Console.WriteLine(JsonResultsFactory.Serialise(shown, total));
        }
        else
        {
            foreach (var match in shown)
            {
                Console.WriteLine(match.DisplayForm);
            }

            if (shown.Count < total)
            {
                // Stderr, so a pipe to wc or grep sees only answers.
                Console.Error.WriteLine(
                    $"words: showing the {shown.Count:N0} most likely of {total:N0} answers. "
                    + "Use --limit 0 for all.");
            }
        }

        return total > 0 ? ExitCodes.Found : ExitCodes.NothingFound;
    }
}
