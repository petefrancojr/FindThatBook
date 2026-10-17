using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using FindThatBook.Api.Features.Books.Search.DTOs;
using FindThatBook.Api.Features.Books.Search.Helpers;
using FindThatBook.Api.Features.Books.Search.Models;
using FindThatBook.Api.Integrations.Gemini;
using FindThatBook.Api.Integrations.Gemini.Models;
using FindThatBook.Api.Integrations.OpenLibrary;
using FindThatBook.Api.Integrations.OpenLibrary.Exceptions;
using FindThatBook.Api.Integrations.OpenLibrary.Models;

namespace FindThatBook.Api.Features.Books.Search.Services;

public class BookSearchService(IGeminiClient gemini, IOpenLibraryClient openLibrary, ILogger<BookSearchService> logger) : IBookSearchService
{
    private const int MaxReadings = 8;

    /// <summary>The brief's limit: at most five results.</summary>
    private const int MaxResults = 10;

    /// <summary>How many works to ask Open Library for per reading. More than <see cref="MaxResults"/>, so ranking has candidates to choose from.</summary>
    private const int MaxWorksPerSearch = 10;

    private static readonly string ReadQueryInstruction = $"""
        You read messy library search text and work out which book the person is looking for.
        The text may hold a title, an author, a character's name, a year, special keywords, or a mix of these,
        often misspelled, abbreviated or in any order.

        Return {MaxReadings} readings of the search, most likely first.
        - title: the book's full, correctly spelled title, only if the search points to one.
        - author: the author's full, correctly spelled name, only if the search points to one.
        - keywords: every other useful word from the search, such as character names, places,
          subjects or a year, spelled correctly. If a word from the search refers to the author or the title,
          then it must be omitted from the keywords list.
        Leave title or author out rather than guess. Never invent a book the search gives no clue to.

        Make sure to include a reading with a title and an author, but no keywords.
        Make sure to include a reading with a title and no keywords and no author.
        Make sure to include a reading with a title and keywords, but no author.
        Make sure to include a reading with an author and no keywords or title.
        Make sure to include a reading with an author and keywords, but no title.
        """;

    // Must stay above ReadingsSchema: static fields are set up in the order they're written.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    /// <summary>
    /// The JSON shape Gemini must answer in, generated from <see cref="QueryReadings"/> so the two can't drift apart.
    /// </summary>
    private static readonly JsonNode ReadingsSchema = JsonSchemaExporter.GetJsonSchemaAsNode(
        JsonOptions,
        typeof(QueryReadings),
        new JsonSchemaExporterOptions
        {
            // List items can't carry nullable annotations, so treat them as non-null rather than "object or null".
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = AddReadingRules,
        });

    public async Task<BookSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var normalizedReadings = await GetReadingsFromGeminiAsync(query, cancellationToken);

        var openLibraryResponse = await GetBooksFromOpenLibraryAsync(normalizedReadings, query.ToNormalized(), cancellationToken);

        logger.LogInformation("\nTotal Unique Result Count: {total}\n", openLibraryResponse.Count);

        var results = openLibraryResponse
            .Select(BookResultMapper.ToBookResult)
            .OfType<BookResult>()
            //.Take(MaxResults)
            .ToList();

        // Use Gemeni to Rank & Explain openLibraryResponse

        return new BookSearchResponse { Query = query, Results = results };
    }

    /// <summary>
    /// Searches Open Library once per reading, all at the same time, and returns the works in reading order
    /// (most likely reading first), each work once. When Gemini gave no readings, the normalized search is used instead.
    ///
    /// Consolidate works into canonicle works openLibraryResponse
    ///
    /// Resolve primary authors from 'works.authors'
    /// </summary>
    private async Task<IReadOnlyList<WorkSearchDoc>> GetBooksFromOpenLibraryAsync(IReadOnlyList<QueryReading> readings, string normalizedQuery, CancellationToken cancellationToken)
    {
        var requests = readings.Select(ToSearchRequest).DistinctBy(Describe).ToList();
        if (requests.Count == 0)
        {
            requests = [new WorkSearchRequest { Query = normalizedQuery, Limit = MaxWorksPerSearch }];
        }

        // Task.WhenAll keeps the order it was given, so the most likely reading's works still come first.
        var searches = await Task.WhenAll(requests.Select(request => SearchWorksAsync(request, cancellationToken)));
        var uniqueResults = searches
            .SelectMany(docs => docs)
            .DistinctBy(doc => doc.Key)
            .ToList();

        // TODO: Update the Rank and Explanation for each uniqueResult. Gemeni should assist with this.

        // TODO: Sort by Rank

        return uniqueResults;
    }

    /// <summary>
    /// Turns a reading into an Open Library search that keeps what Gemini worked out: the title goes in
    /// Open Library's title field, the author in its author field, the keywords in the keywords field.
    /// </summary>
    private static WorkSearchRequest ToSearchRequest(QueryReading reading) => new()
    {
        Title = reading.Title,
        Author = reading.Author,
        Query = reading.Keywords.Count > 0
            ? string.Join(' ', reading.Keywords)
            : null,
        Limit = MaxWorksPerSearch,
    };

    /// <summary>
    /// Sends one search to Open Library and logs it with every work it found. A search Open Library refuses,
    /// such as q=the or a q under 3 characters, finds no works: it isn't an outage, and asking again would be refused again.
    /// </summary>
    private async Task<IReadOnlyList<WorkSearchDoc>> SearchWorksAsync(WorkSearchRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkSearchDoc> docs;
        try
        {
            docs = (await openLibrary.SearchWorksAsync(request, cancellationToken)).Docs;
        }
        catch (OpenLibraryQueryRejectedException ex)
        {
            logger.LogInformation("Open Library refused the search {Search}: {Reasons}", Describe(request), string.Join("; ", ex.Reasons));
            return [];
        }

        logger.LogInformation(
            "\nOpen Library search {Search} found {Count} works:\n{Works}\n",
            Describe(request),
            docs.Count,
            string.Join("\n", docs.Select(doc => $"  {doc.WorkId} \"{doc.Title}\" by {string.Join(", ", doc.AuthorNames)} ({doc.FirstPublishYear})")));
        return docs;
    }

    /// <summary>A search's fields as they are sent, for example: title="the hobbit" author="j r r tolkien".</summary>
    private static string Describe(WorkSearchRequest request) =>
        string.Join(' ', new[]
        {
            request.Title is null ? null : $"title=\"{request.Title}\"",
            request.Author is null ? null : $"author=\"{request.Author}\"",
            request.Query is null ? null : $"q=\"{request.Query}\"",
        }.OfType<string>());

    /// <summary>
    /// Sends the query-reading prompt to Gemini and turns the JSON into normalized readings.
    /// A reading left with no title, author or keywords is dropped.
    /// </summary>
    private async Task<IReadOnlyList<QueryReading>> GetReadingsFromGeminiAsync(string query, CancellationToken cancellationToken)
    {
        var response = await gemini.GenerateContentAsync(new GenerateContentRequest
        {
            SystemInstruction = new Content { Parts = [new Part { Text = ReadQueryInstruction }] },
            Contents = [new Content { Role = "user", Parts = [new Part { Text = query }] }],
            GenerationConfig = new GenerationConfig
            {
                Temperature = 0,
                ResponseMimeType = "application/json",
                ResponseJsonSchema = ReadingsSchema,
            },
        }, cancellationToken);

        if (response.Text is null)
        {
            logger.LogWarning(
                "Gemini gave no readings for {Query}. Finish reason: {FinishReason}. Block reason: {BlockReason}.",
                query,
                response.Candidates.FirstOrDefault()?.FinishReason,
                response.PromptFeedback?.BlockReason);
            return [];
        }

        try
        {
            var readings = JsonSerializer.Deserialize<QueryReadings>(response.Text, JsonOptions)?.Readings ?? [];
            var normalizedReadings = readings
                .Select(NormalizeReading)
                .Where(reading => reading.Title is not null || reading.Author is not null || reading.Keywords.Count > 0)
                .ToList();

            foreach (var reading in normalizedReadings)
            {
                logger.LogInformation("\n\n===========\nTitle:{title}\nAuthor:{author}\nKeyWords: [{keywords}]\n===========\n\n", reading.Title, reading.Author, string.Join(",\n", reading.Keywords));
            }

            return normalizedReadings;            
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Gemini's readings for {Query} were not valid JSON.", query);
            return [];
        }
    }

    /// <summary>
    /// Runs Gemini's title, author and keywords through the same normalization as the search, so they can be
    /// compared with it and with Open Library's data. A title or author that normalizes to nothing becomes null,
    /// and keywords that are empty or repeat another keyword once normalized are dropped.
    /// </summary>
    private static QueryReading NormalizeReading(QueryReading reading) => new()
    {
        Title = NullIfEmpty(reading.Title.ToNormalized()),
        Author = NullIfEmpty(reading.Author.ToNormalized()),
        Keywords = reading.Keywords
            .Select(keyword => keyword.ToNormalized())
            .Where(keyword => keyword.Length > 0)
            .Distinct()
            .ToList(),
    };

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;

    /// <summary>
    /// Adds what the class alone can't say: every non-nullable property is required,
    /// and Gemini returns at most <see cref="MaxReadings"/> readings.
    /// </summary>
    private static JsonNode AddReadingRules(JsonSchemaExporterContext context, JsonNode schema)
    {
        if (schema is not JsonObject node)
        {
            return schema;
        }

        if (context.TypeInfo.Kind == JsonTypeInfoKind.Object)
        {
            var required = context.TypeInfo.Properties.Where(p => !p.IsGetNullable).Select(p => (JsonNode)p.Name).ToArray();
            if (required.Length > 0)
            {
                node["required"] = new JsonArray(required);
            }
        }

        if (context.PropertyInfo?.Name == "readings")
        {
            node["maxItems"] = MaxReadings;
        }

        return node;
    }
}
