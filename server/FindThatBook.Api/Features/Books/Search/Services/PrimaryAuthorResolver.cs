using System.Text.RegularExpressions;
using FindThatBook.Api.Features.Books.Search.Helpers;
using FindThatBook.Api.Features.Books.Search.Models;
using FindThatBook.Api.Integrations.OpenLibrary.Models;

namespace FindThatBook.Api.Features.Books.Search.Services;

/// <summary>
/// Works out who wrote each work and who only contributed to it. Open Library's author list can mix the two:
/// Dixon's graphic novel of The Hobbit lists Charles Dixon, Sean Deming and J.R.R. Tolkien, but only Tolkien
/// wrote the story. Work records carry no roles either, so these rules use what search results do carry.
/// </summary>
/// <remarks>
/// For each person listed on a work, in order, the first rule that applies decides:
/// <list type="number">
/// <item>A role in the work's contributor list that means they didn't write it ("Adapter", "Illustrator",
/// "Translator") makes them a contributor. "Narrator" and "Editor" don't count: co-authors are often tagged
/// with them too, for example a co-author who narrated the audiobook. Roles that would mark everyone listed
/// are ignored, since someone wrote the book: authors who illustrated their own book are tagged that way too.</item>
/// <item>When someone listed is the only author of a bigger work with the same main title, that work is the
/// original: they are primary, and the others listed here are contributors. "Bigger" means more editions,
/// so a small stray record crediting one co-author can't demote the others.</item>
/// <item>A work with a single listed author: that author is primary.</item>
/// <item>Otherwise nobody is guessed: everyone stays a listed author. If rule 1 found contributors and only one
/// person is left, though, that person wrote the book.</item>
/// </list>
/// Measured on live data: on 20 classics, 108 of 111 extra names (adapters, illustrators) became contributors and
/// no real author was demoted; on 12 co-written books, none of 26 co-authors was demoted.
/// </remarks>
public static partial class PrimaryAuthorResolver
{
    /// <summary>Roles that mean the person didn't write the book. Compared with the first word of the role, normalized.</summary>
    private static readonly HashSet<string> NotWritingRoles =
    [
        "adapter", "adaptation", "adapted",
        "illustrator", "illustrations", "illustrated", "ill",
        "translator", "translation", "translated",
    ];

    public static IReadOnlyList<BookCandidate> Resolve(IReadOnlyList<WorkSearchDoc> works)
    {
        var originals = FindOriginals(works);
        return works
            .Select(work => new BookCandidate { Work = work, Authors = ResolveAuthors(work, originals) })
            .ToList();
    }

    /// <summary>
    /// For each main title and author, the most editions of a work with that title whose only author they are.
    /// </summary>
    private static Dictionary<(string MainTitle, string AuthorId), int> FindOriginals(IReadOnlyList<WorkSearchDoc> works)
    {
        var originals = new Dictionary<(string, string), int>();
        foreach (var work in works.Where(work => work.AuthorKeys.Count == 1))
        {
            var key = (WorkConsolidator.MainTitle(work.Title), work.AuthorKeys[0]);
            originals[key] = Math.Max(originals.GetValueOrDefault(key), work.EditionCount ?? 0);
        }

        return originals;
    }

    private static IReadOnlyList<CandidateAuthor> ResolveAuthors(WorkSearchDoc work, Dictionary<(string MainTitle, string AuthorId), int> originals)
    {
        // One person can have two author records ('Salem's Lot lists Stephen King twice), so each name counts once.
        var listed = work.AuthorNames
            .Select((name, index) => (Name: name, AuthorId: index < work.AuthorKeys.Count ? work.AuthorKeys[index] : null))
            .DistinctBy(person => PersonKey(person.Name))
            .ToList();
        var roles = NotWritingRolesByPerson(work.Contributors);

        // Someone must have written the book. Authors who illustrated their own book (Tolkien drew The Hobbit's
        // pictures) can be tagged "Illustrator", so roles that would mark everyone listed are ignored.
        if (listed.All(person => roles.ContainsKey(PersonKey(person.Name))))
        {
            roles.Clear();
        }

        var mainTitle = WorkConsolidator.MainTitle(work.Title);
        var editions = work.EditionCount ?? 0;

        bool WroteBiggerWork(string? authorId) =>
            authorId is not null && originals.GetValueOrDefault((mainTitle, authorId)) > editions;

        var hasOriginalAuthor = listed.Any(person => WroteBiggerWork(person.AuthorId));

        var authors = listed.Select(person =>
        {
            if (roles.TryGetValue(PersonKey(person.Name), out var role))
            {
                return Credit(person, AuthorCredit.Contributor, $"role in contributor: {role}", role);
            }

            if (hasOriginalAuthor)
            {
                return WroteBiggerWork(person.AuthorId)
                    ? Credit(person, AuthorCredit.Primary, $"only author of a bigger work titled \"{mainTitle}\"")
                    : Credit(person, AuthorCredit.Contributor, $"another listed author is the only author of a bigger work titled \"{mainTitle}\"");
            }

            return listed.Count == 1
                ? Credit(person, AuthorCredit.Primary, "only listed author")
                : Credit(person, AuthorCredit.Listed, "nothing shows whether they wrote it");
        }).ToList();

        // When the roles marked everyone else as a contributor, the one person left wrote the book. With more than
        // one left, the others may be contributors without a recorded role (translators often are), so none is guessed.
        if (authors.Any(author => author.Credit == AuthorCredit.Contributor)
            && authors.Count(author => author.Credit == AuthorCredit.Listed) == 1)
        {
            authors = authors
                .Select(author => author.Credit == AuthorCredit.Listed
                    ? new CandidateAuthor { Name = author.Name, AuthorId = author.AuthorId, Credit = AuthorCredit.Primary, Reason = "everyone else listed is a contributor" }
                    : author)
                .ToList();
        }

        return authors;
    }

    private static CandidateAuthor Credit((string Name, string? AuthorId) person, AuthorCredit credit, string reason, string? role = null) =>
        new() { Name = person.Name, AuthorId = person.AuthorId, Credit = credit, Role = role, Reason = reason };

    /// <summary>
    /// Reads "Charles Dixon (Adapter)" as Charles Dixon with the role Adapter, keeping only roles that mean the
    /// person didn't write the book. Entries without a role, such as "Tolkien, J. R. R. 1892-1973.", are skipped.
    /// </summary>
    private static Dictionary<string, string> NotWritingRolesByPerson(IReadOnlyList<string> contributors)
    {
        var roles = new Dictionary<string, string>();
        foreach (var contributor in contributors)
        {
            var match = RoledContributorPattern().Match(contributor);
            if (!match.Success)
            {
                continue;
            }

            var role = match.Groups["role"].Value.Trim();
            var roleWord = role.ToNormalized().Split(' ').FirstOrDefault() ?? "";
            if (NotWritingRoles.Contains(roleWord))
            {
                roles.TryAdd(PersonKey(match.Groups["name"].Value), role);
            }
        }

        return roles;
    }

    /// <summary>
    /// The same key for "Charles Dixon" and "Dixon, Charles.": library records put the surname first, and
    /// sometimes add dates ("Wenzel, David, 1950-"), which are dropped.
    /// </summary>
    private static string PersonKey(string name)
    {
        var parts = name.Split(',', 2);
        var ordered = parts.Length == 2 ? $"{parts[1]} {parts[0]}" : name;
        return string.Join(' ', ordered.ToNormalized().Split(' ').Where(word => !word.All(char.IsDigit)));
    }

    [GeneratedRegex(@"^(?<name>.+?)\s*\((?<role>[^)]+)\)\s*$")]
    private static partial Regex RoledContributorPattern();
}
