using System.CommandLine;
using System.CommandLine.Help;
using Words.Cli;

// `words anagram`, `words add` and `words licence` arrive in phases 4 and 6.
// See docs/plan-cli.md.
var root = new RootCommand("Crossword and anagram solver.");
root.Subcommands.Add(PatternCommand.Create());
root.Subcommands.Add(LexiconCommand.Create());

var parseResult = root.Parse(args);

// Help and parse-error output are the only things carrying a usage line, and both are
// small and produced once — so they can be buffered and tidied. Query results are not
// buffered; they stream straight to the console.
var carriesUsageLine = parseResult.Action is HelpAction || parseResult.Errors.Count > 0;

int exitCode;

if (carriesUsageLine)
{
    var output = new StringWriter();
    var error = new StringWriter();

    exitCode = await parseResult
        .InvokeAsync(new InvocationConfiguration { Output = output, Error = error })
        .ConfigureAwait(false);

    Console.Error.Write(HelpText.WithQuotesOutsidePlaceholders(error.ToString()));
    Console.Out.Write(HelpText.WithQuotesOutsidePlaceholders(output.ToString()));
}
else
{
    exitCode = await parseResult.InvokeAsync().ConfigureAwait(false);
}

// Exit codes follow grep: 0 found, 1 nothing found, 2 something wrong with the request.
// System.CommandLine reports its own parse failures as 1, which would tell a script "no
// matches" when the command was actually malformed.
return parseResult.Errors.Count > 0 ? 2 : exitCode;
