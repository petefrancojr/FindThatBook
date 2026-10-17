using FindThatBook.Api.Features.Books.Search.Candidates;
using FindThatBook.Api.Features.Books.Search.Pipeline.Reading;
using FindThatBook.Api.Integrations.OpenLibrary.Models;

namespace FindThatBook.Api.Tests.TestSupport;

/// <summary>Builds Open Library works, candidates and readings for tests, with only the fields a test cares about.</summary>
public static class Books
{
    /// <summary>
    /// A search result. Authors are given as "Name|OLxxxA"; a bare name gets an ID made from the name,
    /// so the same name always gets the same ID.
    /// </summary>
    public static WorkSearchDoc Work(
        string id,
        string title,
        string[] authors,
        int editions = 1,
        string? subtitle = null,
        int? year = null,
        long? coverId = null,
        string[]? contributors = null) => new()
        {
            Key = $"/works/{id}",
            Title = title,
            Subtitle = subtitle,
            AuthorNames = authors.Select(author => author.Split('|')[0]).ToList(),
            AuthorKeys = authors.Select(author => author.Contains('|') ? author.Split('|')[1] : $"OL-{author}-A").ToList(),
            EditionCount = editions,
            FirstPublishYear = year,
            CoverId = coverId,
            Contributors = contributors ?? [],
        };

    /// <summary>A candidate whose people are already credited, for tests that skip author resolution.</summary>
    public static BookCandidate Candidate(WorkSearchDoc work, params (string Name, AuthorCredit Credit, string? Role)[] people) => new()
    {
        Work = work,
        Authors = people
            .Select(person => new CandidateAuthor { Name = person.Name, Credit = person.Credit, Role = person.Role, Reason = "test" })
            .ToList(),
    };

    public static (string, AuthorCredit, string?) Primary(string name) => (name, AuthorCredit.Primary, null);

    public static (string, AuthorCredit, string?) Listed(string name) => (name, AuthorCredit.Listed, null);

    public static (string, AuthorCredit, string?) Contributor(string name, string role) => (name, AuthorCredit.Contributor, role);

    public static QueryReading ReadingOf(string? title = null, string? author = null, params string[] keywords) =>
        new() { Title = title, Author = author, Keywords = keywords };
}
