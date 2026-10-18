using FindThatBook.Api.Features.Books.Search.Candidates;
using FindThatBook.Api.Features.Books.Search.Pipeline.Consolidation;
using FindThatBook.Api.Features.Books.Search.Pipeline.Reading;
using FindThatBook.Api.Features.Books.Search.Text;

namespace FindThatBook.Api.Features.Books.Search.Pipeline.Ranking;

/// <summary>
/// Gives each candidate a tier in the brief's matching hierarchy by comparing it with the most likely reading,
/// and orders the candidates by tier. The tiers are decided here by fixed rules, not by Gemini, so the same
/// search always gets the same tiers.
/// </summary>
/// <remarks>
/// The reading's author is matched against the work's primary authors at one of three levels: exact (the same name
/// once normalized), loose (part of the name, or a small misspelling) or none. Listed authors, whose role Open Library
/// doesn't show, don't count. Only when no primary author matches is the reading's author looked for among the
/// contributors, for tier B.
/// <list type="table">
/// <item><term>A</term><description>Exact title, and an exact or loose match with a primary author.
/// With no author in the reading, an exact title and a work with a primary or listed author.</description></item>
/// <item><term>B</term><description>Exact title, but the reading's author only matches a contributor.
/// With no author in the reading, an exact title on a work whose people are all contributors.</description></item>
/// <item><term>C</term><description>Near title (missing or extra words, a small misspelling), and the reading's
/// author matches a primary author or a contributor.</description></item>
/// <item><term>D</term><description>The reading has an author but no title, and the author matches a primary
/// author: the author-only fallback. A reading with a title that doesn't match is E, so an unrelated book by
/// someone with the same surname doesn't rank as a match.</description></item>
/// <item><term>E</term><description>Anything else, including every candidate when there is no reading.</description></item>
/// </list>
/// Within a tier, an exact author match comes before a loose one, and otherwise works keep the order they were found in.
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
            .ThenBy(candidate => candidate.Match!.Author == AuthorMatch.Loose ? 1 : 0)
            .ToList();
    }

    /// <summary>
    /// The names to look for on each work: the reading's author. Gemini sometimes keeps a person's name as a keyword
    /// instead ("dixon hobbit" gives the keyword "dixon"), so without an author, a keyword counts as a person when it
    /// names someone on a work whose title matches the reading's. That keeps out a character's name such as "bennet",
    /// which can match an unrelated author called Bennet but no one on Pride and Prejudice. A reading with no title
    /// has nothing to narrow by, so its keywords are checked against every candidate.
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

        // Without a title, a keyword can name someone on any candidate.
        var sameTitle = reading.Title is null
            ? candidates
            : candidates.Where(candidate => CompareTitle(reading.Title, candidate) is TitleMatch.Exact or TitleMatch.Near).ToList();
        return reading.Keywords
            .Where(keyword => sameTitle.Any(candidate => candidate.Authors.Any(author => CompareNames(keyword, author.Name) != AuthorMatch.None)))
            .ToList();
    }

    private static CandidateMatch Compare(string? readingTitle, IReadOnlyList<string> authorNames, BookCandidate candidate)
    {
        var title = readingTitle is null ? TitleMatch.NotGiven : CompareTitle(readingTitle, candidate);

        if (authorNames.Count == 0)
        {
            return new CandidateMatch { Title = title, Author = AuthorMatch.NotGiven };
        }

        var (author, matchedAuthor) = BestMatch(candidate.PrimaryAuthors, authorNames);
        if (author != AuthorMatch.None)
        {
            return new CandidateMatch { Title = title, Author = author, MatchedAuthor = matchedAuthor };
        }

        var (contributor, matchedContributor) = BestMatch(candidate.Contributors, authorNames);
        return new CandidateMatch
        {
            Title = title,
            Author = AuthorMatch.None,
            Contributor = contributor,
            MatchedContributor = matchedContributor,
        };
    }

    /// <summary>The closest match between the names and the people, exact before loose.</summary>
    private static (AuthorMatch Level, CandidateAuthor? Person) BestMatch(IEnumerable<CandidateAuthor> people, IReadOnlyList<string> names)
    {
        var best = (Level: AuthorMatch.None, Person: (CandidateAuthor?)null);
        foreach (var person in people)
        {
            foreach (var name in names)
            {
                var level = CompareNames(name, person.Name);
                if (level == AuthorMatch.Exact)
                {
                    return (level, person);
                }

                if (level == AuthorMatch.Loose && best.Level == AuthorMatch.None)
                {
                    best = (level, person);
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Exact when the names are the same once normalized ("j r r tolkien" and "J.R.R. Tolkien"). Loose when every
    /// word of the shorter name matches a word of the longer one, allowing a small misspelling in longer words:
    /// "tolkien" and "tolkein" both loosely match "J.R.R. Tolkien".
    /// </summary>
    private static AuthorMatch CompareNames(string first, string second)
    {
        var firstKey = PersonNames.Key(first);
        var secondKey = PersonNames.Key(second);
        if (firstKey.Length == 0 || secondKey.Length == 0)
        {
            return AuthorMatch.None;
        }

        if (firstKey == secondKey)
        {
            return AuthorMatch.Exact;
        }

        var firstWords = firstKey.Split(' ');
        var secondWords = secondKey.Split(' ');
        var (shorter, longer) = firstWords.Length <= secondWords.Length ? (firstWords, secondWords) : (secondWords, firstWords);
        return shorter.All(word => longer.Any(other => WordsAlike(word, other))) ? AuthorMatch.Loose : AuthorMatch.None;
    }

    /// <summary>
    /// The same word, or a misspelling of it: one changed letter in a word of 4 or 5 letters, two in a longer word.
    /// Initials and short words must match exactly.
    /// </summary>
    private static bool WordsAlike(string first, string second)
    {
        if (first == second)
        {
            return true;
        }

        var longest = Math.Max(first.Length, second.Length);
        var allowed = longest >= 6 ? 2 : longest >= 4 ? 1 : 0;
        return allowed > 0 && EditDistance(first, second) <= allowed;
    }

    private static string TierFor(CandidateMatch match, BookCandidate candidate)
    {
        var writtenBySomeone = candidate.Authors.Any(author => author.Credit is AuthorCredit.Primary or AuthorCredit.Listed);
        var primaryMatched = match.Author is AuthorMatch.Exact or AuthorMatch.Loose;
        var contributorMatched = match.Contributor is AuthorMatch.Exact or AuthorMatch.Loose;

        return match.Title switch
        {
            TitleMatch.Exact when primaryMatched => "A",
            TitleMatch.Exact when contributorMatched => "B",
            TitleMatch.Exact when match.Author == AuthorMatch.NotGiven => writtenBySomeone ? "A" : "B",
            TitleMatch.Near when primaryMatched || contributorMatched => "C",
            TitleMatch.NotGiven when primaryMatched => "D",
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
