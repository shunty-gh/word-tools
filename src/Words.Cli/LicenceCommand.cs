using System.CommandLine;
using Words.Core;

namespace Words.Cli;

/// <summary>
/// <c>words licence</c> — reproduces the terms of the bundled word lists.
/// </summary>
/// <remarks>
/// Both sources require their notice to accompany anything distributed, so this is a
/// release requirement rather than a courtesy.
/// </remarks>
internal static class LicenceCommand
{
    public static Command Create()
    {
        var command = new Command("licence", "Show the licences of the bundled word lists.");

        // The word lists themselves are American and British in origin; accept either
        // spelling rather than make anyone guess which was used.
        command.Aliases.Add("license");

        command.SetAction(_ =>
        {
            Console.WriteLine("The word lists bundled with this program are used under the following terms.");

            foreach (var licence in LexiconLicences.All)
            {
                Console.WriteLine();
                Console.WriteLine(licence.Source);
                Console.WriteLine(new string('=', licence.Source.Length));
                Console.WriteLine();
                Console.WriteLine(licence.Summary);
                Console.WriteLine();
                Console.WriteLine(licence.Text);
            }

            return ExitCodes.Found;
        });

        return command;
    }
}
