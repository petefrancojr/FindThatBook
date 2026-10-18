using FindThatBook.Api.Features.Books.Search.Candidates;
using FindThatBook.Api.Features.Books.Search.Pipeline.Explanation;
using FindThatBook.Api.Features.Books.Search.Pipeline.Finding;
using FindThatBook.Api.Features.Books.Search.Pipeline.Reading;
using FindThatBook.Api.Features.Books.Search.Services;
using FindThatBook.Api.Integrations.OpenLibrary.Models;
using Microsoft.Extensions.Logging.Abstractions;
using static FindThatBook.Api.Tests.TestSupport.Books;

namespace FindThatBook.Api.Tests.Features.Books.Search.Services;

/// <summary>
/// Requirement 1: accept sparse, dense and ambiguous plain-text searches. Requirement 4e and "Return results":
/// an ordered list of candidates with explanations.
/// </summary>
public class BookSearchServiceTests
{
    private static readonly WorkSearchDoc Hobbit = Work("OL27482W", "The Hobbit", ["J.R.R. Tolkien|OL26320A"], editions: 482, year: 1937);
    private static readonly WorkSearchDoc DixonsHobbit = Work("OL219602W", "The Hobbit", ["Charles Dixon|OL2453782A", "J.R.R. Tolkien|OL26320A"],
        editions: 10, year: 1990, contributors: ["Charles Dixon (Adapter)"]);
    private static readonly WorkSearchDoc Silmarillion = Work("OL27495W", "The Silmarillion", ["J.R.R. Tolkien|OL26320A"], editions: 100, year: 1977);

    private static BookSearchService CreateService(
        IReadOnlyList<QueryReading> readings,
        IReadOnlyList<WorkSearchDoc> works,
        RecordingExplainer? explainer = null,
        RecordingReader? reader = null) =>
        new(
            reader ?? new RecordingReader(readings),
            new FixedFinder(works),
            explainer ?? new RecordingExplainer(),
            NullLogger<BookSearchService>.Instance);

    [Fact]
    public async Task SendsTheSearchToTheReaderAsTypedAndEchoesIt()
    {
        var reader = new RecordingReader([]);

        var response = await CreateService([], [], reader: reader).SearchAsync("Tolkien Hobbit, illustrated 1937");

        Assert.Equal("Tolkien Hobbit, illustrated 1937", Assert.Single(reader.Queries));
        Assert.Equal("Tolkien Hobbit, illustrated 1937", response.Query);
    }

    [Fact]
    public async Task ReturnsNoBooksForASearchWithNoLettersOrDigits()
    {
        var reader = new RecordingReader([]);

        var response = await CreateService([], [Hobbit], reader: reader).SearchAsync("!!!");

        Assert.Empty(response.Results);
        Assert.Empty(reader.Queries);
    }

    [Fact]
    public async Task ReturnsTheBooksBestMatchFirst()
    {
        var response = await CreateService([ReadingOf("the hobbit", "j r r tolkien")], [Silmarillion, DixonsHobbit, Hobbit]).SearchAsync("tolkien hobbit");

        Assert.Equal(["OL219602W", "OL27482W", "OL27495W"], response.Results.Select(result => result.OpenLibraryId));
        Assert.Equal(["A", "A", "E"], response.Results.Select(result => result.Tier));
    }

    [Fact]
    public async Task RanksAgainstTheFirstReading()
    {
        var response = await CreateService([ReadingOf("the hobbit", "charles dixon"), ReadingOf("the hobbit", "j r r tolkien")], [Hobbit, DixonsHobbit])
            .SearchAsync("dixon hobbit");

        Assert.Equal("OL219602W", response.Results[0].OpenLibraryId);
        Assert.Equal("B", response.Results[0].Tier);
    }

    [Fact]
    public async Task MergesDuplicateWorksIntoOneBook()
    {
        var duplicate = Work("OL45346371W", "The Hobbit", ["J.R.R. Tolkien|OL26320A"], editions: 2);

        var response = await CreateService([ReadingOf("the hobbit", "j r r tolkien")], [duplicate, Hobbit]).SearchAsync("tolkien hobbit");

        Assert.Equal("OL27482W", Assert.Single(response.Results).OpenLibraryId);
    }

    [Fact]
    public async Task NamesThePrimaryAuthorOfAnAdaptation()
    {
        var response = await CreateService([ReadingOf("the hobbit", "charles dixon")], [DixonsHobbit]).SearchAsync("dixon hobbit");

        Assert.Equal("J.R.R. Tolkien", Assert.Single(response.Results).Author);
    }

    [Fact]
    public async Task ReturnsAtMostTenBooks()
    {
        var works = Enumerable.Range(1, 15).Select(index => Work($"OL{index}W", $"Book {index}", ["Someone|OL1A"])).ToArray();

        var response = await CreateService([ReadingOf(author: "someone")], works).SearchAsync("someone");

        Assert.Equal(10, response.Results.Count);
    }

    [Fact]
    public async Task ExplainsOnlyTheBooksThatAreReturned()
    {
        var explainer = new RecordingExplainer();
        var works = Enumerable.Range(1, 15).Select(index => Work($"OL{index}W", $"Book {index}", ["Someone|OL1A"])).ToArray();

        await CreateService([ReadingOf(author: "someone")], works, explainer).SearchAsync("someone");

        Assert.Equal(10, explainer.Explained.Count);
    }

    [Fact]
    public async Task ReturnsTheExplanationForEachBook()
    {
        var explainer = new RecordingExplainer(candidate => $"Explained {candidate.Work.WorkId}.");

        var response = await CreateService([ReadingOf("the hobbit", "j r r tolkien")], [Hobbit], explainer).SearchAsync("tolkien hobbit");

        Assert.Equal("Explained OL27482W.", Assert.Single(response.Results).Explanation);
    }

    [Fact]
    public async Task SearchesTheNormalizedQueryWhenThereAreNoReadings()
    {
        var finder = new FixedFinder([]);
        var service = new BookSearchService(new RecordingReader([]), finder, new RecordingExplainer(), NullLogger<BookSearchService>.Instance);

        await service.SearchAsync("Tale Two Cities!");

        Assert.Equal("tale two cities", finder.NormalizedQueries.Single());
    }

    private class RecordingReader(IReadOnlyList<QueryReading> readings) : IQueryReader
    {
        public List<string> Queries { get; } = [];

        public Task<IReadOnlyList<QueryReading>> ReadAsync(string query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult(readings);
        }
    }

    private class FixedFinder(IReadOnlyList<WorkSearchDoc> works) : ICandidateFinder
    {
        public List<string> NormalizedQueries { get; } = [];

        public Task<IReadOnlyList<WorkSearchDoc>> FindAsync(IReadOnlyList<QueryReading> readings, string normalizedQuery, CancellationToken cancellationToken = default)
        {
            NormalizedQueries.Add(normalizedQuery);
            return Task.FromResult(works);
        }
    }

    private class RecordingExplainer(Func<BookCandidate, string?>? explain = null) : ICandidateExplainer
    {
        public List<BookCandidate> Explained { get; } = [];

        public Task<IReadOnlyList<BookCandidate>> ExplainAsync(string query, QueryReading? reading, IReadOnlyList<BookCandidate> candidates, CancellationToken cancellationToken = default)
        {
            Explained.AddRange(candidates);
            IReadOnlyList<BookCandidate> explained = candidates
                .Select(candidate => new BookCandidate
                {
                    Work = candidate.Work,
                    Authors = candidate.Authors,
                    Tier = candidate.Tier,
                    Match = candidate.Match,
                    Explanation = explain?.Invoke(candidate),
                })
                .ToList();
            return Task.FromResult(explained);
        }
    }
}
