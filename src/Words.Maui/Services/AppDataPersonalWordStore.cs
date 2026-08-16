using Words.Core;

namespace Words.Maui.Services;

/// <summary>
/// Personal words in the app's data directory, one entry per line.
/// </summary>
/// <remarks>
/// The same plain-text format the CLI writes, so the two can share a file if it is ever
/// synced. <see cref="FileSystem.AppDataDirectory"/> is the per-platform location that
/// survives app updates and is backed up.
/// </remarks>
public sealed class AppDataPersonalWordStore : IPersonalWordStore
{
    private static string Path => System.IO.Path.Combine(FileSystem.AppDataDirectory, "personal.txt");

    public async ValueTask<IReadOnlyList<string>> ReadLinesAsync(CancellationToken cancellationToken = default) =>
        File.Exists(Path)
            ? await File.ReadAllLinesAsync(Path, cancellationToken).ConfigureAwait(false)
            : [];

    public async ValueTask AddAsync(string displayForm, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayForm);

        await File.AppendAllTextAsync(
            Path,
            displayForm.Trim() + Environment.NewLine,
            cancellationToken).ConfigureAwait(false);
    }
}
