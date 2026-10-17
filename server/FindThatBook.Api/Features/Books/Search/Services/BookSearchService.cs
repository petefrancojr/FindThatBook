using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using FindThatBook.Api.Features.Books.Search.Helpers;
using FindThatBook.Api.Features.Books.Search.Models;
using FindThatBook.Api.Integrations.Gemini;
using FindThatBook.Api.Integrations.Gemini.Models;

namespace FindThatBook.Api.Features.Books.Search.Services;

public class BookSearchService(IGeminiClient gemini, ILogger<BookSearchService> logger) : IBookSearchService
{
    private const int MaxReadings = 3;

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
        var normalizedQuery = query.ToNormalized();

        var readings = await GetReadingsFromGeminiAsync(normalizedQuery, cancellationToken);

        foreach(var reading in readings)
        {
            logger.LogInformation("\n\n===========\nTitle:{title}\nAuthor:{author}\nKeyWords: [{keywords}]\n===========\n\n", reading.Title, reading.Author, string.Join(",\n", reading.Keywords));
        }

        // Returns placeholder results until Open Library is wired in and the readings are used.
        return DummyBookData.Search(query);
    }

    /// <summary>Sends the query-reading prompt to Gemini, logs its answer and turns the JSON into readings.</summary>
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
            return JsonSerializer.Deserialize<QueryReadings>(response.Text, JsonOptions)?.Readings ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Gemini's readings for {Query} were not valid JSON.", query);
            return [];
        }
    }

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
