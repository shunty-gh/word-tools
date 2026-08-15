using System.CommandLine;
using Words.Cli;

// The solver commands — pattern, anagram, add, licence — arrive in phase 6.
// See docs/plan-cli.md.
var root = new RootCommand("Crossword and anagram solver.");
root.Subcommands.Add(LexiconCommand.Create());

return await root.Parse(args).InvokeAsync().ConfigureAwait(false);
