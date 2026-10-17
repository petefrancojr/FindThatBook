namespace FindThatBook.Api.Features.Books.Search.Text;

/// <summary>Compares people's names the way they appear in searches and in library records.</summary>
public static class PersonNames
{
    /// <summary>
    /// The same key for "Charles Dixon" and "Dixon, Charles.": library records put the surname first, and
    /// sometimes add dates ("Wenzel, David, 1950-"), which are dropped.
    /// </summary>
    public static string Key(string name)
    {
        var parts = name.Split(',', 2);
        var ordered = parts.Length == 2 ? $"{parts[1]} {parts[0]}" : name;
        return string.Join(' ', ordered.ToNormalized().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(word => !word.All(char.IsDigit)));
    }

    /// <summary>
    /// True when the names refer to the same person as far as text can tell: every word of the shorter name
    /// appears in the longer one, so "tolkien" and "j r r tolkien" match "J.R.R. Tolkien".
    /// </summary>
    public static bool Match(string first, string second)
    {
        var firstWords = Key(first).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var secondWords = Key(second).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (firstWords.Length == 0 || secondWords.Length == 0)
        {
            return false;
        }

        var (shorter, longer) = firstWords.Length <= secondWords.Length ? (firstWords, secondWords) : (secondWords, firstWords);
        return shorter.All(longer.Contains);
    }
}
