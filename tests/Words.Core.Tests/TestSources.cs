using Words.Core;

namespace Words.Core.Tests;

/// <summary>A lexicon source with a fixed set of entries.</summary>
internal sealed class FakeSource(string name, params Entry[] entries) : ILexiconSource
{
    public string Name { get; } = name;

    public ValueTask<IReadOnlyList<Entry>> LoadAsync(CancellationToken cancellationToken = default) =>
        new((IReadOnlyList<Entry>)entries);
}

internal static class TestLexicon
{
    /// <summary>A lexicon of plain entries, all ordinary and unremarkable.</summary>
    public static ValueTask<Lexicon> OfAsync(params string[] displayForms) =>
        Of([.. displayForms.Select(d => Entry.Create(d, 50, Sources.Esdb))]);

    public static ValueTask<Lexicon> Of(params Entry[] entries) =>
        Lexicon.LoadAsync([new FakeSource("test", entries)]);
}
