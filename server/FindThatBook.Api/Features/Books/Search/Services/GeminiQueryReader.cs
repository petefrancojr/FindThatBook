using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using FindThatBook.Api.Features.Books.Search.Helpers;
using FindThatBook.Api.Features.Books.Search.Models;
using FindThatBook.Api.Integrations.Gemini;
using FindThatBook.Api.Integrations.Gemini.Models;

namespace FindThatBook.Api.Features.Books.Search.Services;

/// <summary>Reads a search with Gemini: the prompt, the JSON schema it must answer in, and normalizing its answer.</summary>
public class GeminiQueryReader(IGeminiClient gemini, ILogger<GeminiQueryReader> logger) : IQueryReader
{
    private const int MaxReadings = 9;

    private static readonly string ReadQueryInstruction = $"""
        You read messy library search text and work out which book the person is looking for.
        The text may hold a title, an author, a character's name, a year, special keywords, or a mix of these,
        often misspelled, abbreviated or in any order.

        Return {MaxReadings} readings of the search, most likely first. You are allowed to provide duplicate readings only after all possibile arrangments have been exhausted.
        - title: the book's full, correctly spelled title, only if the search points to one.
        - author: the author's full, correctly spelled name, only if the search points to one.
        - keywords: the other useful words from the search, such as character names, places,
          subjects or a year, spelled correctly. If a word from the search is an author or the title,
          then it must be omitted from the keywords list.

        Only use words and terms the search provides. You may correct their spelling and complete a partial title
        or name that is in the search ("hobbit" can become "The Hobbit"), but never add a title, author or keyword
        that is not in the search. Leave title or author out rather than guess.

        Readings that split the search in different ways help find the book:
        - Include a reading with all terms as keywords.
        - If you can identify a title, include a reading with just the title.
        - If you can identify an author, include a reading with just the author.
        - If you can identify both, include a reading with just the title and the author.
        - If you can identify a title and keywords, include a reading with just the title and the keywords.
        - If you can identify a title, include a reading with the title as a keyword then make the title null.
        - If you can identify an author and keywords, include a reading with the author and the keywords.
        - If you can identify an author, include a reading with the author as a keyword then make the author null.
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

    /// <summary>
    /// Sends the query-reading prompt to Gemini and turns the JSON into normalized readings.
    /// A reading left with no title, author or keywords is dropped.
    /// </summary>
    public async Task<IReadOnlyList<QueryReading>> ReadAsync(string query, CancellationToken cancellationToken = default)
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
