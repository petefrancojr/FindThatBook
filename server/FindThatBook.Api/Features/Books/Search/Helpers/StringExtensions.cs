using System.Globalization;
using System.Text;

namespace FindThatBook.Api.Features.Books.Search.Helpers;

public static class StringExtensions
{
    /// <summary>Letters with no accent to strip, so Unicode decomposition leaves them as they are.</summary>
    private static readonly Dictionary<Rune, string> LetterReplacements = new()
    {
        [new Rune('ø')] = "o",
        [new Rune('æ')] = "ae",
        [new Rune('œ')] = "oe",
        [new Rune('ß')] = "ss",
        [new Rune('ł')] = "l",
        [new Rune('đ')] = "d",
        [new Rune('ð')] = "d",
        [new Rune('þ')] = "th",
        [new Rune('ħ')] = "h",
        [new Rune('ı')] = "i",
    };

    /// <summary>Every kind of apostrophe. Each one becomes a plain "'".</summary>
    private static readonly HashSet<Rune> Apostrophes =
    [
        new Rune('\''),
        new Rune('‘'), // ‘
        new Rune('’'), // ’
        new Rune('ʼ'), // ʼ
        new Rune('`'),
    ];

    /// <summary>
    /// Puts search text into the form Open Library's search matches best, so small differences don't stop a match:
    /// lowercase, no accents, no punctuation except apostrophes inside words, single spaces.
    /// For example "  J.R.R. Tolkien — The Hobbit!" becomes "j r r tolkien the hobbit".
    /// </summary>
    /// <remarks>
    /// In order:
    /// <list type="number">
    /// <item>Unicode compatibility forms become plain ones: full-width "Ｔｏｌｋｉｅｎ", ligatures like "ﬁ" and superscripts like "²".</item>
    /// <item>Accents are removed from Latin letters ("Brontë" is "bronte"), and Latin letters that have none get a plain
    /// spelling ("Ø" is "o", "ß" is "ss"). Open Library's author and title fields don't match "Søren" or "straße".
    /// Marks in other scripts are kept, since they can make a different letter.</item>
    /// <item>Everything is lowercased, which also stops "AND", "OR" and "NOT" being read as search operators.</item>
    /// <item>Apostrophes inside a word are kept as a plain "'": Open Library finds "o'brien" and "o'connor" far more
    /// often than "obrien" or "o brien". Apostrophes at the start or end of a word are dropped.</item>
    /// <item>Full stops, "&amp;" and any other punctuation, symbol or emoji become a space. Initials stay apart
    /// ("J.R.R." is "j r r"), because Open Library's author field finds nothing for "jrr tolkien".</item>
    /// <item>Invisible characters such as zero-width spaces and soft hyphens are removed.</item>
    /// <item>All whitespace (tabs, new lines, non-breaking spaces) collapses to single spaces, trimmed at both ends.</item>
    /// </list>
    /// Letters and digits in any script are kept. Small words such as "a" and "of" are kept too:
    /// they can be a whole title ("It"), so dropping them is left to matching rather than to the text itself.
    /// </remarks>
    public static string ToNormalized(this string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = new StringBuilder(text.Length);
        var afterLatinLetter = false;
        foreach (var rune in text.Normalize(NormalizationForm.FormKD).EnumerateRunes())
        {
            switch (Rune.GetUnicodeCategory(rune))
            {
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.EnclosingMark:
                    // Strip accents from Latin letters only. In other scripts the mark can make a different
                    // letter (Russian "й" is not "и"), so it stays and is recombined below.
                    if (!afterLatinLetter)
                    {
                        cleaned.Append(rune.ToString());
                    }
                    continue;
                case UnicodeCategory.Format:
                    continue;
            }

            afterLatinLetter = false;

            // Checked before letters: Unicode counts some apostrophes, such as "ʼ", as letters.
            if (Apostrophes.Contains(rune))
            {
                cleaned.Append('\'');
                continue;
            }

            if (Rune.IsLetterOrDigit(rune))
            {
                afterLatinLetter = IsLatin(rune);
                var lower = Rune.ToLowerInvariant(rune);
                if (LetterReplacements.TryGetValue(lower, out var replacement))
                {
                    cleaned.Append(replacement);
                }
                else
                {
                    cleaned.Append(lower.ToString());
                }
            }
            else
            {
                cleaned.Append(' ');
            }
        }

        // Apostrophes only count inside a word: "'dune'" is "dune", and a lone "'" disappears.
        var words = cleaned.ToString()
            .Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim('\''))
            .Where(word => word.Length > 0);
        return string.Join(' ', words);
    }

    /// <summary>Basic Latin through Latin Extended-B, Latin Extended Additional, and Latin Extended-C and -D.</summary>
    private static bool IsLatin(Rune rune) =>
        rune.Value <= 0x024F
        || rune.Value is >= 0x1E00 and <= 0x1EFF
        || rune.Value is >= 0x2C60 and <= 0x2C7F
        || rune.Value is >= 0xA720 and <= 0xA7FF;
}
