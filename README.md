# Find That Book

A small library discovery app. A reader types a messy search such as `austen bennet` and gets back the books it most likely refers to.

- **Server:** .NET 10 Web API (`server/`)
- **Client:** Angular (`client/`)

## Running

The only requirement is Docker. .NET and Node run inside containers, so neither needs to be installed locally.

```
docker compose up --build
```

- Web app: http://localhost:4200
- API: http://localhost:5080

## Running the tests

```
docker compose run --rm tests
```

This runs the server's xUnit tests in the .NET 10 SDK container. The `tests` service is in a Compose profile, so `docker compose up` doesn't start it.

## API

| Endpoint | Description |
|---|---|
| `GET /api/books/search?q={text}` | Searches for books. Returns `400` if `q` is missing, blank, or over 200 characters, and `502` if Open Library or Gemini is unavailable. |
| `GET /api/health` | Reports that the API is running. |

Search currently returns dummy data.

## Design decisions

### Code is organized by feature, not by type

The server groups code by feature (a vertical slice) instead of keeping all controllers in one folder and all models in another:

```
server/FindThatBook.Api/Features/
├── Books/
│   └── Search/
│       ├── Controllers/
│       ├── Models/
│       └── Services/     business logic
└── Health/
    └── Controllers/
```

Everything related to one feature sits in one place. Changing how book search works means opening one folder, not hunting through several. A new feature is added as a new folder next to the existing ones and doesn't touch them.

The top-level folder is named `Features` rather than `Domains`. In clean architecture and domain-driven design, "domain" refers to the core layer of business entities and rules, which contains no controllers or HTTP code. Naming this folder `Domains` would suggest that meaning even though each folder also holds its controllers. `Features` is the conventional name for this layout. It also fits technical endpoints like Health, which isn't a business domain.

### API response models are kept apart from external data

Open Library and Gemini will sit next to `Search` inside the Books feature. Each will own its client and a `Models/` folder that mirrors that service's JSON:

```
Features/Books/
├── BooksFeature.cs        registers the feature's services
├── Search/
│   ├── Controllers/
│   ├── Models/            the API contract: what /api/books/search returns
│   └── Services/          search logic: calls the clients, maps their data, ranks
├── OpenLibrary/
│   ├── OpenLibraryClient.cs
│   └── Models/            Open Library's JSON, field for field
└── Gemini/
    ├── GeminiClient.cs
    └── Models/            Gemini's request/response wrappers and the JSON we ask it to return
```

Rules for this layout:

- **Each `Models/` folder has one owner.** `Search/Models` is our public contract and changes only when we change the API. `OpenLibrary/Models` and `Gemini/Models` copy another service's JSON and change only when that service does.
- **External models stop at the search service.** `BookSearchService` maps Open Library and Gemini data into our own types. Controllers and API responses never expose them, so a change on Open Library's side doesn't change our API.
- **The mapping step holds the real decisions.** Merging editions into works and separating primary authors from contributors happens while mapping Open Library data. Keeping that mapping in its own class makes it easy to find and to test with recorded Open Library JSON.
- **External models are named after their source,** for example `WorkRecord` and `AuthorRecord`, so they aren't confused with our `BookResult`.

The clients stay inside `Features/Books` because only Books uses them. If another feature ever needs Open Library, the folder moves up a level.
