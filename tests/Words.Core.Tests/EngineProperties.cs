using CsCheck;
using Words.Core;

namespace Words.Core.Tests;

/// <summary>
/// Properties that must hold for any lexicon and any query.
/// </summary>
/// <remarks>
/// Each case generates a small lexicon and then derives its query from an entry in it, so
/// the properties cannot pass by finding nothing. A five-letter alphabet keeps anagrams and
/// compositions common enough to exercise the interesting paths.
/// </remarks>
public class EngineProperties
{
    private static Gen<string> Word => Gen.String[Gen.Char['a', 'e'], 3, 6];

    private static Gen<string[]> Words => Word.Array[4, 30];

    private static Lexicon Build(IEnumerable<string> words) =>
        Lexicon.LoadAsync([new FakeSource("generated", [.. words.Distinct().Select(w => Entry.Create(w, 50, Sources.Esdb))])])
            .AsTask()
            .GetAwaiter()
            .GetResult();

    private static List<Match> Query(Lexicon lexicon, PatternQuery query) =>
        [.. new WordEngine(lexicon).QueryAsync(query).ToBlockingEnumerable()];

    private static List<Match> Query(Lexicon lexicon, AnagramQuery query) =>
        [.. new WordEngine(lexicon).QueryAsync(query).ToBlockingEnumerable()];

    /// <summary>Normalises before sorting: search keys are uppercase, raw input is not.</summary>
    private static string Sorted(string letters) => SearchKeys.ToCanonical(SearchKeys.From(letters));

    [Fact]
    public void EveryAnagramAnswerIsAPermutationOfTheLettersGiven()
    {
        Gen.Select(Words, Gen.Int[0, 10_000]).Sample((words, pick) =>
        {
            var lexicon = Build(words);
            var target = words[pick % words.Length];

            var matches = Query(lexicon, new AnagramQuery { Letters = target });

            foreach (var match in matches)
            {
                Assert.Equal(Sorted(target), Sorted(match.Components[0].SearchKey));
            }

            // Not vacuous: the entry the letters came from must come back.
            Assert.Contains(matches, m => m.DisplayForm == target);
        });
    }

    [Fact]
    public void ABlankAddsExactlyOneLetterToTheAnswer()
    {
        Gen.Select(Words, Gen.Int[0, 10_000], Gen.Int[0, 10_000]).Sample((words, pick, drop) =>
        {
            var lexicon = Build(words);
            var target = words[pick % words.Length];

            // Remove one letter and replace it with a blank: the entry must be findable
            // again, and every answer must be exactly as long as the target.
            var removeAt = drop % target.Length;
            var letters = target.Remove(removeAt, 1) + ".";

            var matches = Query(lexicon, new AnagramQuery { Letters = letters });

            foreach (var match in matches)
            {
                Assert.Equal(target.Length, match.Components[0].SearchKey.Length);
            }

            Assert.Contains(matches, m => m.DisplayForm == target);
        });
    }

    [Fact]
    public void EveryPatternAnswerHasExactlyThePatternsLength()
    {
        Gen.Select(Words, Gen.Int[0, 10_000], Gen.Int[0, 10_000]).Sample((words, pick, mask) =>
        {
            var lexicon = Build(words);
            var target = words[pick % words.Length];

            // Blank out some positions, keeping the rest literal.
            var pattern = string.Concat(target.Select((c, i) => ((mask >> i) & 1) == 1 ? '.' : c));

            var matches = Query(lexicon, new PatternQuery { Pattern = pattern });

            foreach (var match in matches)
            {
                Assert.Equal(pattern.Length, match.Components[0].SearchKey.Length);
            }

            Assert.Contains(matches, m => m.DisplayForm == target);
        });
    }

    [Fact]
    public void EveryPatternAnswerMatchesPositionByPosition()
    {
        Gen.Select(Words, Gen.Int[0, 10_000], Gen.Int[0, 10_000]).Sample((words, pick, mask) =>
        {
            var lexicon = Build(words);
            var target = words[pick % words.Length];
            var pattern = string.Concat(target.Select((c, i) => ((mask >> i) & 1) == 1 ? '.' : c));

            foreach (var match in Query(lexicon, new PatternQuery { Pattern = pattern }))
            {
                var key = match.Components[0].SearchKey;

                for (var i = 0; i < pattern.Length; i++)
                {
                    if (pattern[i] != '.')
                    {
                        Assert.Equal(char.ToUpperInvariant(pattern[i]), key[i]);
                    }
                }
            }
        });
    }

    [Fact]
    public void EveryCompositionAccountsForPreciselyTheLettersSupplied()
    {
        Gen.Select(Words, Gen.Int[0, 10_000], Gen.Int[0, 10_000]).Sample((words, first, second) =>
        {
            var lexicon = Build(words);
            var left = words[first % words.Length];
            var right = words[second % words.Length];
            var letters = left + right;

            var matches = Query(
                lexicon,
                new AnagramQuery { Letters = letters, Compose = CompositionOptions.Default });

            foreach (var match in matches)
            {
                var used = string.Concat(match.Components.Select(c => c.SearchKey));

                Assert.Equal(Sorted(letters), Sorted(used));
            }

            // The pair the letters came from must be among the answers.
            Assert.Contains(matches, m =>
                m.Components.Count == 2
                && m.Components.Select(c => c.DisplayForm).Order().SequenceEqual(new[] { left, right }.Order()));
        });
    }

    [Fact]
    public void CompositionOnlyEverUsesOrdinarySingleWords()
    {
        Gen.Select(Words, Gen.Int[0, 10_000], Gen.Int[0, 10_000]).Sample((words, first, second) =>
        {
            var lexicon = Build(words);
            var letters = words[first % words.Length] + words[second % words.Length];

            var matches = Query(
                lexicon,
                new AnagramQuery { Letters = letters, Compose = CompositionOptions.Default });

            foreach (var component in matches.Where(m => m.IsComposition).SelectMany(m => m.Components))
            {
                Assert.True(CompositionOptions.IsEligibleComponent(component));
                Assert.True(component.SearchKey.Length >= CompositionOptions.Default.MinComponentLength);
            }
        });
    }
}
