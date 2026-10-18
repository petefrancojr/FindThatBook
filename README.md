# Find That Book

A small library discovery app. A reader types a messy search such as `austen bennet` and gets back the books it most likely refers to.

- **Server:** .NET 10 Web API (`server/`)
- **Client:** Angular (`client/`)

## Overview

The reader types one free-text search into the Angular page. The page calls `GET /api/books/search?q=…`, and the API turns the search into an ordered list of books in stages:

```
"tolkien hobbit illustrated deluxe 1937"
   │
   ▼  Reading        Gemini reads the search into readings: { title?, author?, keywords[] }, most likely first
   ▼  Finding        Open Library's /search.json is searched once per reading, up to three at a time
   ▼  Authors        each work's primary authors are told apart from adapters, illustrators and translators
   ▼  Consolidation  duplicate works for the same book are merged into one canonical work
   ▼  Ranking        each work gets a tier, A to E, by comparing it with the most likely reading
   ▼  Explanation    Gemini writes one sentence per returned book, from the facts behind its tier
   ▼  Mapping        the books become the response: title, authors, contributors, year, ID, link, cover, tier, explanation
   │
   ▼
1. The Hobbit · J.R.R. Tolkien · 1937 · A · "Exact title; J.R.R. Tolkien is primary author."
```

Gemini does the two jobs that need language: understanding the messy search, and writing the explanations. Everything that must be the same every time, such as normalizing text, merging duplicates, telling authors from contributors and choosing tiers, is done by rules in code, which the tests check directly. Each stage has its own folder under `server/FindThatBook.Api/Features/Books/Search/Pipeline/`.

## Features

- **One free-text search box** that accepts sparse (`dickens`), dense (`tolkien hobbit illustrated deluxe 1937`) and ambiguous (`austen bennet`) searches.
- **AI reading of the search** with Gemini, returning structured JSON checked against a schema generated from the C# model. See [Overview](#overview).
- **Open Library search** using each reading's title, author and keywords in their own fields. See [The Open Library client returns Open Library's data as it is](#the-open-library-client-returns-open-librarys-data-as-it-is).
- **Text normalization** for case, punctuation, accents, initials and apostrophes, tuned to what Open Library's fields match. See [Search text is normalized before it is read](#search-text-is-normalized-before-it-is-read).
- **Duplicate works merged** into one canonical work, including subtitle variants. See [Duplicate works are merged into one canonical work](#duplicate-works-are-merged-into-one-canonical-work).
- **Primary authors told apart from contributors**, so Dixon's graphic novel of *The Hobbit* credits Tolkien. See [Primary authors are told apart from contributors](#primary-authors-are-told-apart-from-contributors).
- **The brief's matching hierarchy**, tiers A to E, with results ordered best first. See [Tiers are decided by rules; Gemini only explains them](#tiers-are-decided-by-rules-gemini-only-explains-them).
- **A grounded explanation for each book**, naming the primary author and contributors, with a plain sentence from Open Library's fields when Gemini can't give one.
- **Error handling:** `400` for a missing or too-long search, `422` with Open Library's reason when it refuses a search, `502` when Gemini is down or every Open Library search fails, books from the searches that worked when only some fail, and `200` with no books for a search like `!!!`. See [An unavailable external service is a 502](#an-unavailable-external-service-is-a-502).
- **A results page** that shows each book's cover, title linked to Open Library, author, year, ID and explanation, in the API's order, with loading, empty and error states.
- **Tests** for every server requirement in the brief, and for the client. See [Testing strategy](#testing-strategy).

## Running

The only requirement is Docker. .NET and Node run inside containers, so neither needs to be installed locally.

Search uses Google's Gemini API, which needs a key. Get one from [Google AI Studio](https://aistudio.google.com/apikey), then copy `.env.example` to `.env` at the repo root and fill it in:

```
GEMINI_API_KEY=your-key-here
```

`.env` is gitignored, and docker compose reads it automatically. The API starts without a key, but any search that needs Gemini returns `502` until one is set.

```
docker compose up --build
```

- Web app: http://localhost:4200
- API: http://localhost:5080

## Running the tests

```
docker compose run --rm server-tests
docker compose run --rm client-tests
docker compose run --rm --build e2e-tests
```

- `server-tests` runs the server's xUnit tests in the .NET 10 SDK container.
- `client-tests` runs the Angular unit tests with Vitest in a Node container. Tests use jsdom, so no browser is needed.
- `e2e-tests` runs the Playwright tests in `client/e2e/` in a real Chromium browser, against the built site in the `web` container. The search API is answered inside the browser, so these tests need no Gemini key and never call Open Library. `--build` rebuilds the site first, so the tests see the current code. Playwright's Docker image and `@playwright/test` in `client/package.json` must have the same version (1.55.0).

The browser opens in a window on your desktop, and every action is slowed down so you can follow it: Playwright waits 800 ms between actions, and each test leaves its last page on screen for 2 seconds. The window comes from the container through WSLg's display, so this needs Docker Desktop with WSL 2 on Windows 11. To run without a window at full speed, for example in CI:

```
docker compose run --rm -e HEADLESS=true -e SLOW_MO=0 -e END_PAUSE_MS=0 e2e-tests
```

`SLOW_MO` and `END_PAUSE_MS` also change the speed of a visible run, in milliseconds.

Both services are in a Compose profile, so `docker compose up` doesn't start them.

A few server tests call the real Gemini API. They are skipped by default so the suite never depends on the network or spends quota. With a key in `.env`, run them with:

```
docker compose run --rm -e LIVE_TESTS=true server-tests dotnet test FindThatBook.Api.Tests --filter Category=Live
```

The live tests include a small set for the two prompts, sent to Gemini exactly as the app sends them:

- **`GeminiQueryReaderLiveTests`** uses the brief's example searches. It checks that the first reading holds everything from a dense search (`tolkien hobbit illustrated deluxe 1937`), that sparse and ambiguous searches (`dickens`, `austen bennet`) are read, that a partial name and a character are worked out into the book (`mark huckleberry` gives *Huckleberry Finn* or Mark Twain), that a partial title is completed (`tale two cities`), and that no author or title is added that the search didn't give (`hobbit`, `dune`, `austen bennet`).
- **`GeminiCandidateExplainerLiveTests`** checks that an explanation names the primary author and the adapter's role (Dixon's *Hobbit*), doesn't call a listed author "primary" (*Good Omens*), and mentions no year except the book's own.

Gemini's wording changes from run to run, so these check what the prompts promise, not exact sentences. One run makes 13 calls to Gemini. On the free tier, running it several times in a row can hit the quota (`429 RESOURCE_EXHAUSTED`), which also makes the app's searches fail until it resets.

### Testing strategy

Every server requirement in the brief has tests, and none of them touch the network: Gemini and Open Library are replaced by fakes (`FakeGeminiClient`, `FakeOpenLibraryClient`), and the HTTP clients are tested against a stub handler with recorded JSON. The tests mirror the code's folders, so each pipeline stage's tests sit in the matching folder under `FindThatBook.Api.Tests/Features/Books/Search/Pipeline/`.

- **Pure stages are tested directly,** with small built works and candidates (`TestSupport/Books.cs`): authors, consolidation, ranking and mapping.
- **Stages that call a service are tested against a fake of it:** the reader and explainer against a fake Gemini, the finder against a fake Open Library.
- **`BookSearchService` is tested with fakes of its stages,** so each test checks the order and limits of the search on its own.
- **`BookSearchEndpointTests` runs whole searches through ASP.NET and every stage,** with only Gemini and Open Library faked, to check the response the brief describes and its error statuses.
- **Playwright tests check the page in a real browser.** The API is answered with `page.route`, so they check the page alone. `client/e2e/search-button.spec.ts` covers the search button:

| Playwright test | What it checks |
|---|---|
| Disabled until something is typed | Empty or only spaces: disabled. Text: enabled. Cleared again: disabled. |
| Searches for what was typed, trimmed | `"  tolkien hobbit  "` sends one request with `q=tolkien hobbit`, and the book and its explanation appear |
| Enter also searches | Pressing Enter in the field submits, like clicking |
| Blocks a second search while one runs | During a slow answer the button reads "Searching…" and is disabled, then comes back, and only one request is sent |
| No results | Shows "No books matched “zzzz”." |
| Open Library's reason for a `422` | A refused search for `of` shows "Open Library couldn't run this search: Query too short, must be at least 3 characters.", and the button stays usable |
| Unavailable, then retry | A `502` shows "Search is unavailable right now…", and the next click searches again and clears the error |

| Brief requirement | Tests |
|---|---|
| 1. Accept sparse, dense and ambiguous searches | `BookSearchServiceTests`, `BookSearchEndpointTests` (the brief's own examples), `PersonNamesTests` (partial names) |
| 2a. AI extracts `{ title?, author?, keywords[] }` | `GeminiQueryReaderTests`, `GeminiQueryReaderLiveTests` (live) |
| 2b. A grounded "why it matched" explanation | `GeminiCandidateExplainerTests`, `GeminiCandidateExplainerLiveTests` (live), `BookResultMapperTests` (fallback sentence) |
| 3a. Search `/search.json`; fetch `/works`, `/authors`, `/authors/{id}/works` | `OpenLibraryClientTests`, `OpenLibraryCandidateFinderTests` |
| 3b. Normalize: lowercase, punctuation, diacritics, partials | `StringExtensionsTests`, `CandidateRankerTests` (*tale two cities*) |
| 3b.i. Subtitle variants | `WorkConsolidatorTests`, `CandidateRankerTests` |
| 3b.ii. Canonical works and primary authors | `WorkConsolidatorTests`, `PrimaryAuthorResolverTests` |
| 4. Matching hierarchy A to E | `CandidateRankerTests`, `BookSearchServiceTests` |
| Data quality note: contributors are lower signal and explanations name them | `PrimaryAuthorResolverTests`, `CandidateRankerTests` (tier B), `GeminiCandidateExplainerTests` |
| Return results: title, primary author, year, ID and link, cover, explanation, ordered | `BookResultMapperTests`, `BookSearchEndpointTests` |
| Error handling: 400, 422, 502, and partial results when one search fails | `BookSearchEndpointTests`, `ExternalServiceErrorEndpointTests`, `GeminiClientTests`, `OpenLibraryClientTests`, `OpenLibraryCandidateFinderTests`, the client's `search.spec.ts` and `search.http.spec.ts`, and the Playwright tests |

Not covered: the optional AI re-ranking (2c), which isn't built. The brief's "up to 5" results is tested as 10, the limit chosen for this app.

## Regenerating the API client

The Angular services and models in `client/src/api/generated/` are generated by [ng-openapi-gen](https://github.com/cyclosproject/ng-openapi-gen) from the API's OpenAPI document (`/openapi/v1.json`, published in Development). Don't edit them by hand. After changing a controller or a response model, regenerate them:

```
docker compose run --rm api-client
```

This starts the API if needed, waits for it, and rewrites the generated folder. Settings are in `client/ng-openapi-gen.json`. The generated code is committed, so the app builds without a running API.

Three things on the C# side keep the generated code clean, and new endpoints should follow them:

- `[Produces("application/json")]` on the controller gives one method per route instead of one per content type.
- The route `Name` (for example `[HttpGet("search", Name = "SearchBooks")]`) becomes the `operationId`, which becomes the generated method name (`searchBooks`).
- `[ProducesResponseType]` attributes list the real status codes. Without them the document claims every route only returns 200.

The generated client uses `http://api:8080/` as its default root URL because that's where it read the document. `app.config.ts` overrides it with `provideApiConfiguration('')`, so the browser calls `/api/...` on its own site and nginx forwards those requests to the API.

## API

| Endpoint | Description |
|---|---|
| `GET /api/books/search?q={text}` | Searches for books. Returns `400` if `q` is missing, blank, or over 200 characters, `422` with Open Library's reason if Open Library refuses every search made from it, and `502` if Gemini is unavailable or every Open Library search fails. When only some Open Library searches fail, the books the others found are returned. A search with no letters or digits, such as `!!!`, returns `200` with no books. |
| `GET /api/health` | Reports that the API is running. |

Each result follows the brief's example and adds a few fields:

```json
{
  "title": "The Hobbit",
  "author": "J.R.R. Tolkien",
  "authors": ["J.R.R. Tolkien"],
  "contributors": [
    { "name": "Charles Dixon", "role": "Adapter" },
    { "name": "Sean Deming", "role": "Adapter" }
  ],
  "first_publish_year": 1990,
  "open_library_id": "OL219602W",
  "url": "https://openlibrary.org/works/OL219602W",
  "cover_url": "https://covers.openlibrary.org/b/id/8406766-M.jpg",
  "tier": "B",
  "explanation": "Exact title; J.R.R. Tolkien is primary author; Charles Dixon listed as adapter."
}
```

- **`authors`** lists the people who wrote the book: its primary authors, or everyone listed as an author when Open Library doesn't show which of them wrote it (*Good Omens* gives `["Terry Pratchett", "Neil Gaiman"]`).
- **`author`** is the same people joined with commas, kept for the brief's shape.
- **`contributors`** lists people who worked on the book without writing it, with the `role` Open Library recorded, or `null` when the person was recognized as a contributor without one.

Search runs in steps: Gemini reads the search into readings, Open Library is searched once per reading, each work's primary authors are worked out, duplicate works are merged, the works are ranked against the most likely reading using the brief's tiers A to E, and Gemini explains the ten that are returned.

## Assumptions

- **.NET 10 instead of .NET 8.** The brief asks for a .NET 8 Web API. The server targets .NET 10, the current long-term support release. Moving it to .NET 8 would need small changes: for example, the Gemini response schemas are generated with `JsonSchemaExporter`, which arrived in .NET 9. Running everything in Docker means no .NET SDK has to be installed to try it.
- **Gemini is the AI,** as the brief suggests, using the model set in `Gemini:Model` in `appsettings.json` (`gemini-3.5-flash-lite`). Changing the model needs no code change.
- **Up to ten results instead of five.** The brief's tier E says "return up to 5 ordered candidates". Ten gives the reader more to choose from when the search is vague; the limit is one constant, `MaxResults` in `BookSearchService`.
- **The first reading is the most likely one.** Gemini is told to put the complete reading first, with every title, author and keyword it found, and every book is ranked against that reading. The other readings only widen the Open Library search.
- **Nothing is added that the search didn't give.** Gemini may correct spelling and complete a title or name the search names (`hobbit` becomes *The Hobbit*), but not add an author or title the reader didn't type. A search for `hobbit` is therefore ranked on the title alone.
- **Open Library's data is used as it is.** Where its records are incomplete or inconsistent, such as missing roles or duplicate works, the app works around them with rules and doesn't try to correct the data.
- **A refused search is the reader's to change.** When Open Library refuses every search made from the readings (for example `the`), its reason is passed on with a `422` rather than hidden behind an empty result.

## Design decisions

### Code is organized by feature, not by type

The server groups code by feature (a vertical slice) instead of keeping all controllers in one folder and all models in another:

```
server/FindThatBook.Api/
├── Features/
│   ├── Books/
│   │   └── Search/
│   │       ├── Controllers/
│   │       ├── DTOs/         the response the API returns
│   │       ├── Candidates/   what flows between the pipeline stages
│   │       ├── Text/         text helpers several stages use
│   │       ├── Pipeline/     one folder per stage of the search
│   │       └── Services/     BookSearchService: runs the stages in order
│   └── Health/
│       └── Controllers/
├── Integrations/             clients for external services, shared by features
├── Middleware/               request pipeline steps (error handling)
└── Common/                   shared setup and types (OpenAPI, the shared exception base)
```

Everything related to one feature sits in one place. Changing how book search works means opening one folder, not hunting through several. A new feature is added as a new folder next to the existing ones and doesn't touch them.

The top-level folder is named `Features` rather than `Domains`. In clean architecture and domain-driven design, "domain" refers to the core layer of business entities and rules, which contains no controllers or HTTP code. Naming this folder `Domains` would suggest that meaning even though each folder also holds its controllers. `Features` is the conventional name for this layout. It also fits technical endpoints like Health, which isn't a business domain.

### API response models are kept apart from external data

The Open Library and Gemini clients live in a top-level `Integrations/` folder, next to `Features/`. Other features are likely to need them later, so they sit where any feature can find and reuse them. Each one owns its client, behind an interface, and a `Models/` folder that mirrors that service's JSON:

```
server/FindThatBook.Api/
├── Features/Books/
│   ├── BooksFeature.cs        registers the feature's services
│   └── Search/
│       ├── Controllers/       BookSearchController
│       ├── DTOs/              BookSearchResponse, BookResult: the response the API returns
│       ├── Candidates/        BookCandidate, CandidateAuthor, AuthorCredit: a work with its credited people
│       ├── Text/              ToNormalized(), PersonNames
│       ├── Pipeline/          the search's stages, in the order they run:
│       │   ├── Reading/       IQueryReader, GeminiQueryReader, QueryReading(s): the search becomes readings
│       │   ├── Finding/       ICandidateFinder, OpenLibraryCandidateFinder: readings become works
│       │   ├── Authors/       PrimaryAuthorResolver: primary authors apart from contributors
│       │   ├── Consolidation/ WorkConsolidator: duplicate works become one canonical work
│       │   ├── Ranking/       CandidateRanker, CandidateMatch, TitleMatch, AuthorMatch: tiers A to E
│       │   ├── Explanation/   ICandidateExplainer, GeminiCandidateExplainer, CandidateExplanation(s)
│       │   └── Mapping/       BookResultMapper: candidates become the API response
│       └── Services/          BookSearchService, IBookSearchService: run the stages in order
└── Integrations/
    ├── OpenLibrary/
    │   ├── IOpenLibraryClient.cs, OpenLibraryClient.cs
    │   ├── OpenLibraryRegistration.cs   AddOpenLibrary()
    │   └── Models/            Open Library's search request and JSON (search, works, authors), field for field
    └── Gemini/
        ├── IGeminiClient.cs, GeminiClient.cs
        ├── GeminiRegistration.cs        AddGemini()
        └── Models/            Gemini's request/response wrappers
```

Rules for this layout:

- **Integrations stay general.** A client describes what its service can do (search, fetch a work, generate content), never what book search needs. The prompts we send Gemini and the JSON we ask it to return belong to the feature that uses them, which keeps each client reusable.
- **Integrations never reference features.** They don't know about `BookResult`, tiers or each other. A feature injects the interface it needs. Because of this one-way rule, `Integrations/` could later move into its own class library without changes.
- **The search is split into pipeline stages, one folder each.** Each folder under `Search/Pipeline/` holds everything its stage needs: its interface, its implementation and its own models. Working on ranking means opening `Pipeline/Ranking/` and nothing else. The folders follow the order the stages run in `BookSearchService.SearchAsync`, and a new stage, such as an author-works fallback, is a new folder that doesn't touch the others.
- **A model lives with the code that uses it.** Models used by one stage sit in that stage's folder, for example `QueryReading` in `Pipeline/Reading/`. Models that flow between stages sit in `Search/Candidates/`, and the models the API returns sit in `Search/DTOs/`, so they change only when the API does. `OpenLibrary/Models` and `Gemini/Models` copy another service's JSON and change only when that service does.
- **Namespaces follow the folders,** for example `FindThatBook.Api.Features.Books.Search.Pipeline.Ranking`.
- **Each stage that calls an outside service sits behind an interface the feature owns.** `IQueryReader` and `ICandidateExplainer` (Gemini) and `ICandidateFinder` (Open Library) let `BookSearchService` run the stages without knowing how either service is called, and give tests a seam for a fake. The implementations live in the feature, not in `Integrations/`, because the prompts and the way readings become searches are decisions about book search. Stages that are pure logic, such as ranking and mapping, are static classes with no interface.
- **External models stop at the feature.** `BookSearchService` maps Open Library and Gemini data into our own types. Controllers and API responses never expose them, so a change on Open Library's side doesn't change our API.
- **The mapping step holds the real decisions.** Merging editions into works and separating primary authors from contributors happens while mapping Open Library data in the feature. Keeping that mapping in its own class makes it easy to find and to test with recorded Open Library JSON.
- **External models are named after their source,** for example `WorkSearchDoc` (one of the `docs` in Open Library's search response) and `GenerateContentResponse` (Gemini's `generateContent` answer), so they aren't confused with our `BookResult`.

### An unavailable external service is a 502

When Gemini or Open Library can't give an answer, its client throws an exception derived from `ExternalServiceUnavailableException` in `Common/Exceptions`. That covers a network failure, a timeout, a rejected API key, rate limiting or a server error. Each integration keeps its own exceptions in its `Exceptions/` folder. `ExternalServiceExceptionHandler` in `Middleware/` turns it into a `502` ProblemDetails response that names the service ("Gemini is unavailable right now. Try again in a moment."). The underlying reason is logged but never sent to the client.

An answer that arrives but isn't usable, such as a blocked prompt or empty text, is not an error at the client level. The Gemini client returns it with `Text` set to null, and the feature that asked decides what to do, for example falling back to a template explanation.

The Gemini API key is sent in the `x-goog-api-key` header rather than the URL, so it never appears in request logs.

### The Open Library client returns Open Library's data as it is

Like the Gemini client, `IOpenLibraryClient` knows nothing about book search. Matching, merging editions and ranking belong to the feature that calls it. It covers the four endpoints the brief names:

| Method | Endpoint | Returns |
|---|---|---|
| `SearchWorksAsync(WorkSearchRequest)` | [`/search.json`](https://openlibrary.org/dev/docs/api/search) | `WorkSearchResponse`: query, title, author, subject, fields, sort, language and paging |
| `GetWorkAsync(workId)` | `/works/{id}.json` | `WorkRecord`, or null if there is no such work |
| `GetAuthorAsync(authorId)` | `/authors/{id}.json` | `AuthorRecord`, or null if there is no such author |
| `GetAuthorWorksAsync(authorId, limit, offset)` | `/authors/{id}/works.json` | `AuthorWorksResponse`, one page of works, or null if there is no such author |

- **It asks only for the fields it reads.** Without a `fields` parameter Open Library returns every field, including edition and ISBN lists hundreds of entries long. `WorkSearchRequest.DefaultFields` lists the fields `WorkSearchDoc` maps. A caller can pass its own list, for example adding `editions` to get each work's best-matching edition.
- **Field names are mapped one by one.** Open Library mixes `snake_case` (`author_name`, `first_publish_year`) with `camelCase` (`numFound`), so every model property has a `[JsonPropertyName]` instead of relying on a naming policy.
- **Every result field is optional.** Open Library leaves out what a work doesn't have, so strings and numbers are nullable and lists default to empty.
- **IDs come with and without their path.** `key` arrives as `/works/OL27482W`, and every model has a `WorkId`, `AuthorId` or `EditionId` that gives the bare `OL27482W`. The methods that take an ID accept only the bare form, in any case (`ol27482w` works too), and send it upper-cased.
- **IDs are checked before they reach a URL.** `GetWorkAsync` only accepts `OL…W` and the author methods only `OL…A`, so a value from a search result or a user can never change the path being requested. Anything else throws `ArgumentException`, because it is a bug in the caller, not an outage. A sample of 11,245 author keys from live searches all matched.
- **Some search results are editions, not works.** An edition that was never attached to a work still appears in search, under its own edition ID (`/works/OL8866470M`, ending in `M`). There were 80 in a sample of 12,941 results, and 15 in the first 1,000 for "harry potter". Requesting `/works/OL8866470M.json` redirects to the edition record at `/books/`, which has a different shape. `GetWorkAsync` rejects the ID instead of reading an edition as a work, and `WorkSearchDoc.IsWork` lets the caller skip the lookup and use what the search result already holds.
- **`author_name` in search results is not the author list.** It holds every name credited on any edition, including illustrators and translators. `WorkRecord.Authors` is the work's own list, and each `WorkAuthorRole` has a `Role` (such as "Translator") only when the person is a contributor rather than a primary author.
- **Some text fields have two shapes.** `description`, `bio` and `first_sentence` come either as a plain string or as `{ "type": "/type/text", "value": "..." }`, even between two records of the same type (Tolkien's bio is a string, Austen's is an object). `TextValueJsonConverter` reads both into a string. In a sample of 270 work records, 110 descriptions were strings and 99 were objects.
- **Image lists can hold `-1`.** `covers` and `photos` use `-1` to mean "no image", so a caller must skip it before building an image URL.
- **An author's works are not ranked.** `/authors/{id}/works.json` returns works in no useful order (Tolkien's first page is colouring books). For the brief's "top works by that author" fallback, search by author instead, where Open Library ranks by relevance.
- **"Not found" is not an outage.** A missing work or author returns null, so the caller can fall back. A 404 from search does mean an outage, because a search with no matches returns an empty `Docs` list rather than a 404.
- **A refused query is not an outage either.** Open Library answers `422` to queries it won't run: a blocked word on its own (`the`) or anything under 3 characters (`of`, `a`, `*`). The client throws `OpenLibraryQueryRejectedException` with Open Library's reasons, not `OpenLibraryUnavailableException`, because retrying won't help and the service is up. The caller decides what a refusal means for it. Other malformed queries still get a `200` with no results, and a few (a bare `AND`, an unknown `sort`) get a `500`, which can't be told apart from a real outage and stays a 502.
- **It identifies itself.** Open Library asks applications to send a `User-Agent` with their name and, for regular use, a contact address. It is set in `OpenLibrary:UserAgent`.
- **Outages are a 502,** the same as Gemini: an unreachable host, a timeout, an error status other than `404` or `422`, or a body that isn't JSON throws `OpenLibraryUnavailableException`.

### A search Open Library refuses is passed on to the client as a 422

When Open Library refuses a query (a `422`, for `the` or anything under 3 characters), `OpenLibraryClient` throws `OpenLibraryQueryRejectedException` with Open Library's reasons. One user search becomes several Open Library searches, one per reading, so `BookSearchService` decides per search:

- **Some readings refused:** the refused ones are logged and skipped, and the user gets the books the others found.
- **Every reading refused:** `BookSearchService` throws one `OpenLibraryQueryRejectedException` holding all of Open Library's reasons. `QueryRejectedExceptionHandler` in `Middleware/` turns it into a `422` ProblemDetails whose `detail` is Open Library's reason, for example "Query too short, must be at least 3 characters."

A search with no letters or digits at all, such as `!!!`, never reaches Open Library. It returns a `200` with no books.

Design considerations:

- **It isn't an outage.** Open Library answered and is working, and the same query will be refused every time. A `502` saying "Open Library is unavailable, try again" would be wrong, and trying again wouldn't help. That's why the exception doesn't derive from `ExternalServiceUnavailableException`.
- **The reason helps the user.** "Query too short" or "the following queries are not allowed: the" says what to change, so it's passed on rather than hidden behind an empty result.
- **`422` mirrors Open Library.** The request was well formed, and our own validation passed, but the search terms can't be run. That's what `422 Unprocessable Entity` means, and it keeps the response apart from the controller's `400` for a missing or too-long `q`.
- **One refused reading shouldn't hide the books others found.** Gemini can produce a reading that is only a short keyword such as `of`. Failing the whole search because of it would throw away good results.
- **The client stays general.** What a refusal means is the caller's decision, so the client only reports it.

Alternatives considered:

| Alternative | What it would do | Why not |
|---|---|---|
| Treat a refusal as no matches | A `200` with no books | The user wouldn't learn why nothing came back, or what to change. |
| Map it to a `400 Bad Request` | The same status as the controller's validation | It would mix our own validation with Open Library's rules, and the client couldn't tell them apart. |
| Fail when any reading is refused | A `422` even if other readings found books | It throws away good results because of one weak reading. |
| Treat it as an outage (`502`) | Reuse the existing handler | It tells the user to retry something that will fail again, and it would count against Open Library in the logs. |
| Check the query before calling Open Library | Avoid the request entirely | Open Library's rules (which words are blocked, the minimum length) are its own and can change. Copying them would drift. |

### Duplicate works are merged into one canonical work

Open Library often has more than one work record for the same book. A search for "pride and prejudice" returns both `OL66554W` and `OL15165350W`: same title, same author, same year. `WorkConsolidator` merges them before the results are mapped, so each book appears once.

- **Two works are the same book when the main title and the set of author IDs both match.** The main title is the title up to its first `:`, `;` or `[`, normalized. So "Frankenstein; or, The Modern Prometheus" and "Frankenstein" match, and so do Open Library's noted records such as "Pride and Prejudice [1/2]".
- **Round brackets are not cut.** In collections they hold the real titles: "Novels (Emma / Pride and Prejudice)" and "Novels (Northanger Abbey / Pride and Prejudice)" are different books, and cutting at `(` merged them into one.
- **Adaptations stay separate.** Dixon's graphic novel is titled *The Hobbit* too, but its authors differ from Tolkien's novel, so it remains its own result. The brief ranks such works lower; it doesn't hide them.
- **The canonical work is the one with the most editions,** because that is the record Open Library attaches most editions to: `OL66554W` has 4,042, `OL15165350W` has 132. Edition count only chooses between records already known to be the same book, so a wrong pick shows the same book under a different ID rather than a wrong book.
- **Books keep the order they were first found in,** so the most likely reading's works still come first.
- **It is pure logic with no calls,** so it is a static class with no interface, unlike the steps that call Gemini or Open Library.

Measured on live searches: *Pride and Prejudice* 26 works became 23, *Frankenstein* 11 became 9, and *The Hobbit* and *Harry Potter and the Philosopher's Stone* each lost one duplicate.

### Primary authors are told apart from contributors

Open Library's `author_name` can list adapters, illustrators and translators next to the real author. Dixon's graphic novel of *The Hobbit* lists Charles Dixon, Sean Deming and J.R.R. Tolkien. The brief asks for primary authors from `works.authors`, but work records carry no roles: in 144 sampled work records with 332 author entries, none had one. `PrimaryAuthorResolver` uses what search results do carry, with no extra calls.

For each person listed on a work, the first rule that applies decides:

| Rule | Example | Result |
|---|---|---|
| 1. A role in the search result's `contributor` field that means they didn't write it: Adapter, Illustrator, Translator | "Charles Dixon (Adapter)" | Dixon is a contributor |
| 2. Someone listed is the only author of a **bigger** work with the same main title | Tolkien is the only author of *The Hobbit* `OL27482W` (482 editions), bigger than Dixon's (10) | Tolkien is primary; the others on Dixon's work are contributors |
| 3. The work lists one author | *Dune*: Frank Herbert | Herbert is primary |
| 4. Nothing applies | *Good Omens*: Pratchett, Gaiman | Both stay listed authors; nobody is guessed |

The response's `author` is the primary authors, or everyone listed when none is known, so an illustrator is never presented as the writer.

Decisions behind these rules:

- **"Narrator" and "Editor" don't count as "didn't write it".** Co-authors carry them too: Stephen Dubner is tagged "Narrator" on *Freakonomics*, which he co-wrote, and King and Straub are tagged "Editor" on *Black House*.
- **"Bigger" stops stray records from demoting co-authors.** Open Library has a small "Good Omens" record crediting only Gaiman. Without the size check, Pratchett would become a contributor on the 73-edition work they wrote together. Edition count only compares two records here; it never decides authorship on its own.
- **Roles that would mark everyone are ignored,** because someone wrote the book. Authors who illustrated their own book can be tagged "Illustrator".
- **A name listed twice counts once.** *'Salem's Lot* lists Stephen King under two author records.
- **"Whoever is left wrote it" only applies to one person.** On *Tao te Ching*, Legge is tagged "Translator", and the other two names besides Laozi are most likely untagged translators, so none of the three is guessed.
- **It runs before duplicates are merged.** Every work Open Library returned gets its authors resolved, and then `WorkConsolidator` keeps one candidate per book, with that candidate's authors.
- **It is pure logic, so it is a static class,** like `WorkConsolidator`. Each decision is logged at Debug with its reason, for example `Charles Dixon = Contributor (role in contributor: Adapter)`.

Measured on live data before it was built: across 20 classics, 108 of 111 extra names (adapters, illustrators) became contributors and no real author was demoted. Across 12 co-written books, none of 26 co-authors was demoted.

### Tiers are decided by rules; Gemini only explains them

Each canonical work is compared with the **first** reading, which the prompt makes the complete one (every title, author and keyword Gemini identified). `CandidateRanker` gives it a tier from the brief's hierarchy, and the works are ordered by tier, keeping the search order within a tier. `GeminiCandidateExplainer` then writes one sentence for each of the ten that are returned, from the tier and the facts that decided it.

| Title compared with the reading | The reading's author on the work | Tier |
|---|---|---|
| Exact | primary or listed author | **A** |
| Exact | only a contributor | **B** |
| Exact, and the reading names no author | the work has a primary or listed author / only contributors | **A** / **B** |
| Near | in any role | **C** |
| The reading names no title | primary or listed author | **D** |
| Anything else | | **E** |

- **"Exact" ignores case, punctuation, accents, a subtitle and small words** such as "a", "the" and "of", so *Tale of Two Cities* is an exact match for *A Tale of Two Cities*. **"Near"** means missing or extra words (*The Hobbit & The Lord of the Rings*) or a small misspelling (*El hobbit*).
- **Names match loosely:** every word of the shorter name must appear in the longer, so "tolkien" and "j r r tolkien" both match "J.R.R. Tolkien". When several people on a work match, the best credit counts.
- **A person kept as a keyword still counts.** Gemini sometimes reads "dixon hobbit" as the title *The Hobbit* with the keyword "dixon". A keyword counts as the author when it names someone on a work whose title matches the reading's. So "dixon" counts, and a character's name such as "bennet" doesn't, even though unrelated authors are called Bennet.
- **D needs the reading to have no title.** It is the brief's author-only fallback. When the reading has a title and a work doesn't match it, the work is E, so an unrelated book by someone with the same surname doesn't rank as a match.

Why the tiers aren't decided by Gemini: an earlier version asked Gemini for the tier too, and on live searches it applied the rules inconsistently. It gave Tolkien's own *Hobbit* a B for "dixon hobbit", rated Austen's novels E ("no author match") for "jane austen", and named people in explanations who weren't on that work. The tiers are mechanical checks, so code makes them the same every time and they can be tested without Gemini. Gemini does the part it is good at: turning the facts into a sentence such as *"Exact title; Charles Dixon listed as adapter."* It receives only each work's own fields and match facts, and is told never to add anything else or disagree with the tier.

### Search text is normalized before it is read

The brief asks for searches to be normalized (lowercase, punctuation, diacritics, partials) so that small differences don't stop a match. `ToNormalized()` in `Features/Books/Search/Text/StringExtensions.cs` does this, and `BookSearchService` runs it on the user's search, and on the title, author and keywords Gemini reads from it before they are sent to Open Library. When matching against Open Library is built, its titles and author names should go through the same method, so both sides of a comparison follow identical rules.

The rules are chosen for Open Library's search. Each one was checked against the live API, because its `title` and `author` fields are stricter than `q`: a form that `q` forgives can find nothing in a field.

It applies these rules in order:

| Rule | Example |
|---|---|
| Compatibility characters become plain ones | `ＴＯＬＫＩＥＮ` → `tolkien`, `ﬁnding` → `finding`, `Henry Ⅷ` → `henry viii` |
| Accents are removed from Latin letters | `Brontë` → `bronte`, `García Márquez` → `garcia marquez` |
| Latin letters with no accent to strip are spelled plainly | `Søren` → `soren`, `Straße` → `strasse`, `Łódź` → `lodz` |
| Everything is lowercased | `The Hobbit` → `the hobbit` |
| Apostrophes inside a word are kept, as a plain `'` | `O’Brien` → `o'brien`, `Philosopher’s` → `philosopher's` |
| Apostrophes at the start or end of a word are dropped | `'Dune'` → `dune`, `authors'` → `authors` |
| Full stops, `&` and other punctuation, symbols and emoji become spaces | `J.R.R.` → `j r r`, `Pride & Prejudice` → `pride prejudice`, `spider-man` → `spider man`, `Dune: Messiah` → `dune messiah` |
| Invisible characters are removed | zero-width spaces, soft hyphens, byte-order marks |
| All whitespace collapses to single spaces | tabs, new lines and non-breaking spaces |

What Open Library returned for each form (works found):

| Rule | Form we send | Works | Other form | Works |
|---|---|---|---|---|
| Initials stay apart | `author=j r r tolkien` | 352 | `author=jrr tolkien` | 0 |
| | `author=j d salinger` | 44 | `author=jd salinger` | 0 |
| | `author=h g wells` | 9,967 | `author=hg wells` | 12 |
| Apostrophes are kept | `author=tim o'brien` | 132 | `author=tim obrien` | 8 |
| | `author=flannery o'connor` | 101 | `author=flannery oconnor` | 4 |
| | `title=the handmaid's tale` | 57 | `title=the handmaid s tale` | 0 |
| `&` is dropped | `title=war peace` | 5,069 | `title=war and peace` | 3,773 |
| Accents and letters are folded | `author=soren kierkegaard` | 1,102 | `author=Søren Kierkegaard` | 0 |
| | `author=gabriel garcia marquez` | 498 | `author=Gabriel García Márquez` | 0 |
| | `title=die strasse` | 317 | `title=die straße` | 0 |

Decisions behind these rules:

- **Accents are stripped only from Latin letters.** In other scripts a mark can turn one letter into another: Russian `й` is a different letter from `и`, so `Война` must not become `воина`. Cyrillic, Greek, Devanagari and other scripts keep their marks and are only lowercased.
- **Initials are kept apart, not joined.** Open Library's author field indexes `J.R.R. Tolkien` as separate letters, so `jrr tolkien` finds nothing. The cost is that a search typed as `JRR Tolkien` stays `jrr tolkien`. Gemini usually spells the author out properly (`J.R.R. Tolkien`), which normalizes to the form that works. A decimal such as `2.0` becomes `2 0`, which is rare in a book search.
- **Apostrophes are kept inside words.** Names like O'Brien and O'Connor are indexed with the apostrophe, and removing it or turning it into a space loses most of their books. Possessives such as `philosopher's` match as well as `philosophers` does. Every kind of apostrophe (`’`, `‘`, `ʼ`, `` ` ``) becomes a plain `'`, because the curly one finds nothing in a title.
- **A lone apostrophe is never sent.** One at the start or end of a word is dropped, so a stray `'` can't reach Open Library, which refuses it.
- **`&` is dropped rather than turned into "and".** Titles written with `&` don't contain the word "and", so sending it narrows the search. Dropping it finds both spellings.
- **Lowercasing also keeps queries safe.** Open Library reads `AND`, `OR` and `NOT` in capitals as search operators, so `War AND Peace` is a different search from `war and peace`. A bare `AND` even returns a server error.
- **Some apostrophes count as letters in Unicode** (`ʼ`, U+02BC). The apostrophe rule runs before the letter check so they're handled like the others.
- **Small words are kept.** The brief's "partials" example (`tale two cities` should match `A Tale of Two Cities`) is about comparing two texts, not cleaning one. Dropping words like "a" and "of" here would empty titles made entirely of them, such as *It*. Ignoring them is left to the matching step.
- **A search can normalize to nothing.** `!!!` passes the controller's validation, because it isn't blank, but normalizes to an empty string. `BookSearchService` then returns no results straight away, without calling Gemini or Open Library, since neither has anything to work with.
- **Running it twice changes nothing.** The output of `ToNormalized()` is already in normal form, so text that has been cleaned once can safely be cleaned again.
- **The response echoes what the user typed.** `query` in the response is the original search, not the normalized one, so the page can show it back as entered.
- **It is named `ToNormalized()`, not `Normalize()`.** `string` already has an instance method `Normalize()`, and C# always prefers an instance method over an extension method with the same name, so an extension called `Normalize()` would never run.
- **It lives in the feature's `Text/` folder,** not in `Common/`. These rules exist for matching book titles and author names. If another feature needs them later, the class can move to `Common/`.

`StringExtensionsTests` covers each rule, the brief's own examples and the edge cases above.

## Settled search behaviour

- **Readings are searched at most three at a time,** asking for 10 works each. Open Library is a free service that asks for considerate use, so a search with nine readings runs in three rounds instead of nine requests at once. `MaxConcurrentSearches` in `OpenLibraryCandidateFinder` sets the limit.
- **When Gemini gives no readings,** the normalized search is sent to Open Library instead, so a Gemini problem doesn't mean no results.
- **A search that fails is skipped while other searches found books,** so one failure doesn't lose the rest. That covers both a refused search and an outage (Open Library unreachable, too slow or returning an error); the outage is logged as a warning. Only when no search worked does the user hear about it: a `502` if any search hit an outage, since trying again may help, or a `422` with Open Library's reasons if every search was refused.
- **Up to ten results are returned.** The brief's tier E says "up to 5 ordered candidates"; `MaxResults` is 10 by choice.
- **The order is decided on the server only.** `BookSearchService` returns the results best first: by tier, A to E, and within a tier in the order the readings found them. The client shows them exactly as given and never re-sorts, which its tests check. So the ranking rules live in one place, and a change to them never needs a client release.
- **When Gemini can't explain a result** (its answer is blocked or isn't valid JSON), the result keeps its tier and gets a plain sentence built from Open Library's fields instead: *Open Library lists "The Hobbit" by J.R.R. Tolkien, first published in 1937.*

## Future improvements

What I would do with more time: first the known limitations, then the improvements.

### Known limitations

- **With no author in the search, every work with that exact title is tier A.** For `hobbit`, Jude Fisher's and Patty Kempshall's *The Hobbit* rank A next to Tolkien's. Open Library's order keeps Tolkien's first, but the tier doesn't tell them apart.
- **A subtitle can make a different book an exact match.** "The Hobbit: The Desolation of Smaug" has the main title "The Hobbit", so it counts as an exact title for `hobbit`.
- **A search by subtitle alone isn't reliably found.** Open Library's work record for *The Hobbit* has no subtitle, so `title=there and back again` doesn't match it, and Gemini isn't allowed to name a title the search didn't give. A work that does record the subtitle is only a near match (tier C).
- **Primary authors are only as good as Open Library's data.** Work records carry no roles, so a contributor is only recognized when a role is recorded or the original work is among the results. Otherwise everyone listed stays a listed author, and nobody is guessed.
- **The optional AI re-ranking (2c) isn't built.** The order comes from the tiers and Open Library's relevance.
- **Gemini is called twice per search,** once to read it and once to explain the results. That adds latency, and an outage of either call fails the search with a `502`.

### Improvements

#### Weighted readings decide how many results each reading gets

Today every reading is treated the same: Open Library is searched for each one, and within a tier the results follow reading order, so the most likely reading's works crowd out the rest whether Gemini was sure of it or not.

If each reading carried a weight, a confidence between 0 and 1 that it is what the user meant, the ten result slots could be shared out in proportion to those weights:

| Gemini's readings | Weights | Results |
|---|---|---|
| One clear reading | 0.9, 0.1 | 9 books from the first reading, 1 from the second |
| Two equally likely readings | 0.5, 0.5 | Books from both, split evenly |
| Nothing stands out | 0.4, 0.3, 0.3 | A spread across all three readings |

When Gemini is confident, the list concentrates on the books it is sure about. When it isn't, the list covers more possibilities instead of betting everything on one guess. The weights could also order the results and help pick each one's tier.

This would mean:

- **Asking Gemini for a weight** on each reading, by adding a `weight` property to `QueryReading`, so the generated schema asks for it too.
- **Making the weights add up to 1** before using them, since Gemini's numbers won't always.
- **Rounding to whole slots** so every slot is filled, and handing a reading's unused slots to the next one when its search finds fewer works.
- **Not trusting the numbers too much.** Gemini's confidence is a rough signal, not a measured probability, so it should be checked against real searches before it changes the results much.

#### A request ID on every log line and response

One search writes log lines from HTTP logging, Gemini, Open Library and, on failure, the exception handler, and nothing in the console output ties them together. Two steps, each useful on its own:

- **Show logging scopes.** ASP.NET Core already runs each request in a scope holding its own `RequestId` and `TraceId`, but the console logger hides scopes by default. Setting `Logging:Console:FormatterOptions:IncludeScopes` to `true` in `appsettings.json` puts them on every line, with no new code.
- **Return the ID to the caller.** A `RequestIdMiddleware` in `Middleware/` would read an `X-Request-Id` header from the caller, or make a new ID, put it in a logging scope, and send it back in the response's `X-Request-Id` header. A user who sees a `502` could then quote the ID, and it would match the logs.

Details for the middleware:

- **It runs first,** before `UseExceptionHandler()`, so the exception handler's own log line falls inside the scope and error responses carry the header too.
- **It sets the header in `Response.OnStarting`,** because the exception handler clears the response before it writes a `502` or `500`.
- **A caller's ID is used only when it is short and plain** (letters, digits and `._:-`, up to 64 characters). Anything else is replaced, so a crafted header can't write fake lines or control characters into the logs.
- **Its scope should be named `CorrelationId`,** not `RequestId`, because ASP.NET Core's built-in scope already uses `RequestId` and every line would show two different IDs under the same name.

#### Searches in more languages

Search is built for languages written in the Latin alphabet, the ones whose letters can be reduced to plain English letters. `ToNormalized()` folds `Brontë` to `bronte` and `Søren` to `soren`, so a search typed without accents still matches. Other scripts get much less help:

- **Other scripts aren't converted to Latin letters.** Cyrillic, Greek and other scripts are kept and lowercased, but not transliterated, so `Война и мир` and `voyna i mir` don't match each other, and neither matches the English title *War and Peace*.
- **Languages without spaces between words** (Chinese, Japanese, Thai) stay one long block of text, so matching on individual words, as the "partials" rule needs, doesn't work for them.
- **Accents are left on non-Latin letters on purpose,** because there a mark can make a different letter (see the normalization decisions above), so folding can't simply be extended to them.

Supporting more languages would mean transliterating each script into Latin letters (for example with ICU's transliterator), splitting words in scripts that don't use spaces, matching against a work's titles in other languages as well as its main title, and passing the user's language to Open Library through `WorkSearchRequest.Language`.

#### A token limit on Gemini calls

Nothing limits how many tokens Gemini may spend on an answer. The reading prompt asks for a few short readings, so answers are small today, but an answer that runs long costs more, takes longer and uses up the free tier's quota sooner. `GeminiClient` already logs the tokens each call used.

- **Cap each answer.** `GenerationConfig` already has `MaxOutputTokens`; `BookSearchService` doesn't set it yet. The limit should come from the token counts in the logs, with room to spare, and live in configuration (for example `Gemini:MaxOutputTokens`) so it can change without a release.
- **A cut-off answer is already safe.** If Gemini hits the limit mid-answer, it stops with finish reason `MAX_TOKENS` and the JSON is incomplete. `BookSearchService` treats invalid JSON as no readings and falls back to searching the normalized query, so the user still gets results. The warning it logs would show when the limit is too low.
- **The input is already bounded.** The controller rejects searches over 200 characters, so the prompt can't grow with what the user types.
- **A budget over time** (tokens per minute or per day) would go further, refusing or queueing calls once the budget is spent. That matters only when many people use the site.

#### One place to configure the search limits

The three numbers that shape every search are constants in `BookSearchService`:

| Constant | What it limits | Now |
|---|---|---|
| `MaxReadings` | How many readings Gemini returns for one search | 8 |
| `MaxWorksPerSearch` | How many works Open Library returns for each reading's search | 10 |
| `MaxResults` | How many distinct books the client receives | 10 |

Changing any of them means editing code and releasing. Moving them into one `BookSearchOptions` class, bound from a `BookSearch` section in `appsettings.json` the same way `GeminiOptions` and `OpenLibraryOptions` are, would put them in one place and let each environment set its own:

```json
"BookSearch": {
  "MaxReadings": 8,
  "MaxWorksPerSearch": 10,
  "MaxResults": 10
}
```

Things to handle when this is built:

- **Check the values at startup** with `ValidateOnStart`, like the integrations do, so a zero or negative limit stops the app instead of quietly returning nothing.
- **The prompt and the schema depend on `MaxReadings`.** The prompt says "Return {MaxReadings} readings", and the generated JSON schema sets `maxItems` from it. Both are built once into static fields today, so they would need to be built from the options instead.
- **The numbers multiply.** A search makes up to `MaxReadings` Open Library calls of `MaxWorksPerSearch` works each, so 8 × 10 is up to 80 works fetched to return `MaxResults`. Raising either one raises Open Library's load and the time spent waiting on it.
- **The brief asks for at most 5 results.** `MaxResults` is 10 by choice, so the setting should say so where it is configured.
