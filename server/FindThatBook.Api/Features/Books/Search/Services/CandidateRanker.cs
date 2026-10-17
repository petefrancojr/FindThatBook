using FindThatBook.Api.Features.Books.Search.Helpers;
using FindThatBook.Api.Features.Books.Search.Models;

namespace FindThatBook.Api.Features.Books.Search.Services;

/// <summary>
/// Gives each candidate a tier in the brief's matching hierarchy by comparing it with the most likely reading,
/// and orders the candidates by tier. The tiers are decided here by fixed rules, not by Gemini, so the same
/// search always gets the same tiers.
/// </summary>
/// <remarks>
/// <list type="table">
/// <item><term>A</term><description>Exact title, and the reading's author is a primary or listed author.
/// With no author in the reading, an exact title and a work with a primary or listed author.</description></item>
/// <item><term>B</term><description>Exact title, but the reading's author is only a contributor.
/// With no author in the reading, an exact title on a work whose people are all contributors.</description></item>
/// <item><term>C</term><description>Near title (missing or extra words, a small misspelling), and the reading's
/// author is on the work in any role.</description></item>
/// <item><term>D</term><description>The reading has an author but no title, and the author is a primary or
/// listed author: the author-only fallback. A reading with a title that doesn't match is E, so an unrelated book
/// by someone with the same surname doesn't rank as a match.</description></item>
/// <item><term>E</term><description>Anything else, including every candidate when there is no reading.</description></item>
/// </list>
/// </remarks>
public static class CandidateRanker
{
    /// <summary>Words that don't tell titles apart, so "Tale of Two Cities" is an exact match for "A Tale of Two Cities".</summary>
    private static readonly HashSet<string> SmallWords = ["a", "an", "the", "of", "and", "or", "in", "on", "at", "to", "for", "with", "by"];

    /// <summary>How alike two titles must be, from 0 to 1, to count as a near match despite a misspelling.</summary>
    private const double NearSpellingSimilarity = 0.8;

    /// <summary>
    /// Ranks the candidates against the reading. Candidates keep the order they were found in within a tier,
    /// so the most likely reading's works still come first.
    /// </summary>
    public static IReadOnlyList<BookCandidate> Rank(QueryReading? reading, IReadOnlyList<BookCandidate> candidates)
    {
        var authorNames = AuthorNames(reading, candidates);
        return candidates
            .Select(candidate =>
            {
                var match = Compare(reading?.Title, authorNames, candidate);
                return new BookCandidate
                {
                    Work = candidate.Work,
                    Authors = candidate.Authors,
                    Match = match,
                    Tier = TierFor(match, candidate),
                };
            })
            // The tiers run A to E, so alphabetical order is best first. OrderBy is stable, so ties keep their order.
            .OrderBy(candidate => candidate.Tier, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The names to look for on each work: the reading's author. Gemini sometimes keeps a person's name as a keyword
    /// instead ("dixon hobbit" gives the keyword "dixon"), so without an author, a keyword counts as a person when it
    /// names someone on a work whose title matches the reading's. That keeps out a character's name such as "bennet",
    /// which can match an unrelated author called Bennet but no one on Pride and Prejudice.
    /// </summary>
    private static IReadOnlyList<string> AuthorNames(QueryReading? reading, IReadOnlyList<BookCandidate> candidates)
    {
        if (reading is null)
        {
            return [];
        }

        if (reading.Author is not null)
        {
            return [reading.Author];
        }

        if (reading.Title is null)
        {
            return [];
        }

        var sameTitle = candidates.Where(candidate => CompareTitle(reading.Title, candidate) is TitleMatch.Exact or TitleMatch.Near).ToList();
        return reading.Keywords
            .Where(keyword => sameTitle.Any(candidate => candidate.Authors.Any(author => PersonNames.Match(keyword, author.Name))))
            .ToList();
    }

    private static CandidateMatch Compare(string? readingTitle, IReadOnlyList<string> authorNames, BookCandidate candidate)
    {
        var title = readingTitle is null ? TitleMatch.NotGiven : CompareTitle(readingTitle, candidate);

        if (authorNames.Count == 0)
        {
            return new CandidateMatch { Title = title, Author = AuthorMatch.NotGiven };
        }

        var matched = BestCredited(candidate, authorNames);
        return new CandidateMatch { Title = title, Author = matched is null ? AuthorMatch.None : ToAuthorMatch(matched), MatchedAuthor = matched };
    }

    /// <summary>The person on the work matching one of the names, with the best credit when several match.</summary>
    private static CandidateAuthor? BestCredited(BookCandidate candidate, IReadOnlyList<string> names) =>
        candidate.Authors
            .Where(author => names.Any(name => PersonNames.Match(name, author.Name)))
            .OrderBy(author => author.Credit)
            .FirstOrDefault();

    private static AuthorMatch ToAuthorMatch(CandidateAuthor author) =>
        author.Credit switch
        {
            AuthorCredit.Primary => AuthorMatch.Primary,
            AuthorCredit.Listed => AuthorMatch.Listed,
            _ => AuthorMatch.Contributor,
        };

    private static string TierFor(CandidateMatch match, BookCandidate candidate)
    {
        var writtenBySomeone = candidate.Authors.Any(author => author.Credit is AuthorCredit.Primary or AuthorCredit.Listed);

        return (match.Title, match.Author) switch
        {
            (TitleMatch.Exact, AuthorMatch.Primary or AuthorMatch.Listed) => "A",
            (TitleMatch.Exact, AuthorMatch.Contributor) => "B",
            (TitleMatch.Exact, AuthorMatch.NotGiven) => writtenBySomeone ? "A" : "B",
            (TitleMatch.Near, AuthorMatch.Primary or AuthorMatch.Listed or AuthorMatch.Contributor) => "C",
            (TitleMatch.NotGiven, AuthorMatch.Primary or AuthorMatch.Listed) => "D",
            _ => "E",
        };
    }

    /// <summary>
    /// Compares the reading's title with the work's main title, its full title, and its title with its subtitle,
    /// and keeps the best result.
    /// </summary>
    private static TitleMatch CompareTitle(string readingTitle, BookCandidate candidate)
    {
        var work = candidate.Work;
        var forms = new[]
        {
            WorkConsolidator.MainTitle(work.Title),
            work.Title.ToNormalized(),
            $"{work.Title} {work.Subtitle}".ToNormalized(),
        }.Where(form => form.Length > 0).Distinct();

        var best = TitleMatch.None;
        foreach (var form in forms)
        {
            var result = CompareTitles(readingTitle, form);
            if (result == TitleMatch.Exact)
            {
                return TitleMatch.Exact;
            }

            if (result == TitleMatch.Near)
            {
                best = TitleMatch.Near;
            }
        }

        return best;
    }

    /// <summary>Both titles arrive normalized.</summary>
    private static TitleMatch CompareTitles(string reading, string work)
    {
        if (reading == work)
        {
            return TitleMatch.Exact;
        }

        var readingWords = SignificantWords(reading);
        var workWords = SignificantWords(work);
        if (readingWords.Count == 0 || workWords.Count == 0)
        {
            return TitleMatch.None;
        }

        if (readingWords.SequenceEqual(workWords))
        {
            return TitleMatch.Exact;
        }

        // Missing or extra words: one title's words all appear in the other ("dune" and "dune messiah").
        if (readingWords.All(workWords.Contains) || workWords.All(readingWords.Contains))
        {
            return TitleMatch.Near;
        }

        return Similarity(string.Join(' ', readingWords), string.Join(' ', workWords)) >= NearSpellingSimilarity
            ? TitleMatch.Near
            : TitleMatch.None;
    }

    private static List<string> SignificantWords(string title) =>
        title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(word => !SmallWords.Contains(word)).ToList();

    /// <summary>1 for identical text, falling towards 0 as more letters must change: "the hobit" and "the hobbit" score 0.9.</summary>
    private static double Similarity(string first, string second)
    {
        var longest = Math.Max(first.Length, second.Length);
        return longest == 0 ? 1 : 1 - (double)EditDistance(first, second) / longest;
    }

    /// <summary>How many single letters must be added, removed or changed to turn one text into the other.</summary>
    private static int EditDistance(string first, string second)
    {
        var previous = Enumerable.Range(0, second.Length + 1).ToArray();
        var current = new int[second.Length + 1];
        for (var i = 1; i <= first.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= second.Length; j++)
            {
                var change = first[i - 1] == second[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + change);
            }

            (previous, current) = (current, previous);
        }

        return previous[second.Length];
    }
}
