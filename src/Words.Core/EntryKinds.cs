namespace Words.Core;

/// <summary>
/// How an entry is classified, for including or excluding it per query.
/// </summary>
/// <remarks>
/// Derived from the entry's own text rather than supplied by any word list, so these are
/// a good guess and not an authority. <see cref="SingleWord"/> and <see cref="Phrase"/>
/// are mutually exclusive; <see cref="ProperNoun"/> is orthogonal to both.
/// </remarks>
[Flags]
public enum EntryKinds
{
    None = 0,
    SingleWord = 1,
    Phrase = 2,
    ProperNoun = 4,
    All = SingleWord | Phrase | ProperNoun,
}
