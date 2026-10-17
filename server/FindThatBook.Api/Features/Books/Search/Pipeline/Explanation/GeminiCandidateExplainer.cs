using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using FindThatBook.Api.Features.Books.Search.Candidates;
using FindThatBook.Api.Features.Books.Search.Pipeline.Ranking;
using FindThatBook.Api.Features.Books.Search.Pipeline.Reading;
using FindThatBook.Api.Integrations.Gemini;
using FindThatBook.Api.Integrations.Gemini.Models;

namespace FindThatBook.Api.Features.Books.Search.Pipeline.Explanation;

/// <summary>
/// Has Gemini write each candidate's explanation from its tier and the facts that decided it. The tiers are
/// already set by <see cref="CandidateRanker"/>; Gemini only puts them into words.
/// </summary>
public class GeminiCandidateExplainer(IGeminiClient gemini, ILogger<GeminiCandidateExplainer> logger) : ICandidateExplainer
{
    private const string ExplainInstruction = """
        You explain why each book a library search found matched what the person was looking for.
        You get the person's search, the most likely reading of it, and the candidate books. Each candidate has a
        tier that is already decided, the facts it was decided from, and the fields Open Library returned.

        Write one sentence for each candidate that explains its tier from those facts:
        - Say how the title compares with the reading: exact, near, or different.
        - Say how the reading's author is credited on the work: primary author, listed author, only a contributor,
          or not on the work.
        - Name the primary authors, and name contributors with their role when they matter. Only people in
          primary_authors are primary authors. When it is empty, call the people in listed_authors the listed
          authors, and don't say which of them wrote the book.
        For example: "Exact title; Tolkien is primary author; Dixon listed as adapter."

        Write for the person who searched: call the reading "your search", and leave out what the search didn't give
        (if it named no author, don't mention one).
        Use only the facts and fields given for that candidate. Never add anything that is not in them, and never
        disagree with the tier. Use each candidate's id exactly as given.
        """;

    /// <summary>What each tier means, sent with the candidates so the explanation can say why it was given.</summary>
    private static readonly Dictionary<string, string> TierMeanings = new()
    {
        ["A"] = "exact title, and the reading's author is a primary or listed author (or the reading has no author)",
        ["B"] = "exact title, but the reading's author is only a contributor (or everyone on the work is a contributor)",
        ["C"] = "near title, and the reading's author is on the work",
        ["D"] = "author-only match: the reading names an author but no title, and the author is a primary or listed author",
        ["E"] = "no clear match",
    };

    // Must stay above ExplanationsSchema: static fields are set up in the order they're written.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    /// <summary>
    /// The JSON shape Gemini must answer in, generated from <see cref="CandidateExplanations"/> so the two can't drift apart.
    /// </summary>
    private static readonly JsonNode ExplanationsSchema = JsonSchemaExporter.GetJsonSchemaAsNode(
        JsonOptions,
        typeof(CandidateExplanations),
        new JsonSchemaExporterOptions
        {
            // List items can't carry nullable annotations, so treat them as non-null rather than "object or null".
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = AddRequiredProperties,
        });

    public async Task<IReadOnlyList<BookCandidate>> ExplainAsync(string query, QueryReading? reading, IReadOnlyList<BookCandidate> candidates, CancellationToken cancellationToken = default)
    {
        var toExplain = candidates.Where(candidate => candidate.Work.WorkId is not null).ToList();
        if (toExplain.Count == 0)
        {
            return candidates;
        }

        var response = await gemini.GenerateContentAsync(new GenerateContentRequest
        {
            SystemInstruction = new Content { Parts = [new Part { Text = ExplainInstruction }] },
            Contents = [new Content { Role = "user", Parts = [new Part { Text = DescribeCandidates(query, reading, toExplain) }] }],
            GenerationConfig = new GenerationConfig
            {
                Temperature = 0,
                ResponseMimeType = "application/json",
                ResponseJsonSchema = ExplanationsSchema,
            },
        }, cancellationToken);

        if (response.Text is null)
        {
            logger.LogWarning(
                "Gemini gave no explanations for {Query}. Finish reason: {FinishReason}. Block reason: {BlockReason}.",
                query,
                response.Candidates.FirstOrDefault()?.FinishReason,
                response.PromptFeedback?.BlockReason);
            return candidates;
        }

        IReadOnlyList<CandidateExplanation> explanations;
        try
        {
            explanations = JsonSerializer.Deserialize<CandidateExplanations>(response.Text, JsonOptions)?.Explanations ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Gemini's explanations for {Query} were not valid JSON.", query);
            return candidates;
        }

        // An unknown id or an empty sentence is skipped, and that candidate keeps the plain fallback explanation.
        var byId = explanations
            .Where(explanation => explanation.Explanation.Trim().Length > 0)
            .GroupBy(explanation => explanation.Id.Trim())
            .ToDictionary(group => group.Key, group => group.First().Explanation.Trim());

        logger.LogInformation("Gemini explained {Explained} of {Total} candidates", toExplain.Count(candidate => byId.ContainsKey(candidate.Work.WorkId!)), candidates.Count);

        return candidates
            .Select(candidate => candidate.Work.WorkId is not null && byId.TryGetValue(candidate.Work.WorkId, out var explanation)
                ? new BookCandidate
                {
                    Work = candidate.Work,
                    Authors = candidate.Authors,
                    Tier = candidate.Tier,
                    Match = candidate.Match,
                    Explanation = explanation,
                }
                : candidate)
            .ToList();
    }

    /// <summary>
    /// The user's message: the search, the reading, and each candidate's tier, the facts behind it and the fields
    /// Open Library returned, as JSON. Nothing else is sent, so the explanations can't draw on anything else.
    /// </summary>
    private static string DescribeCandidates(string query, QueryReading? reading, IReadOnlyList<BookCandidate> candidates) =>
        JsonSerializer.Serialize(new
        {
            search = query,
            reading = reading is null ? null : new { title = reading.Title, author = reading.Author, keywords = reading.Keywords },
            candidates = candidates.Select(candidate => new
            {
                id = candidate.Work.WorkId,
                tier = candidate.Tier,
                tier_meaning = candidate.Tier is null ? null : TierMeanings.GetValueOrDefault(candidate.Tier),
                title_match = candidate.Match?.Title switch
                {
                    TitleMatch.Exact => "exact",
                    TitleMatch.Near => "near",
                    TitleMatch.None => "different",
                    _ => "the reading has no title",
                },
                reading_author_on_work = candidate.Match?.Author switch
                {
                    AuthorMatch.Primary => "primary author",
                    AuthorMatch.Listed => "listed author",
                    AuthorMatch.Contributor => "only a contributor",
                    AuthorMatch.None => "not on the work",
                    _ => "the reading has no author",
                },
                matched_person = candidate.Match?.MatchedAuthor is { } person ? new { name = person.Name, role = person.Role } : null,
                title = candidate.Work.Title,
                subtitle = candidate.Work.Subtitle,
                first_publish_year = candidate.Work.FirstPublishYear,
                primary_authors = candidate.PrimaryAuthors.Select(author => author.Name),
                listed_authors = candidate.Authors.Where(author => author.Credit == AuthorCredit.Listed).Select(author => author.Name),
                contributors = candidate.Contributors.Select(author => new { name = author.Name, role = author.Role }),
            }),
        }, JsonOptions);

    /// <summary>Adds what the class alone can't say: every non-nullable property is required.</summary>
    private static JsonNode AddRequiredProperties(JsonSchemaExporterContext context, JsonNode schema)
    {
        if (schema is not JsonObject node || context.TypeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return schema;
        }

        var required = context.TypeInfo.Properties.Where(p => !p.IsGetNullable).Select(p => (JsonNode)p.Name).ToArray();
        if (required.Length > 0)
        {
            node["required"] = new JsonArray(required);
        }

        return node;
    }
}
