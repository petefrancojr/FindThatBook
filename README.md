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
│   ├── Controllers/
│   ├── Models/
│   └── Services/     business logic
└── Health/
    └── Controllers/
```

Everything related to one feature sits in one place. Changing how book search works means opening one folder, not hunting through several. A new feature is added as a new folder next to the existing ones and doesn't touch them.

The top-level folder is named `Features` rather than `Domains`. In clean architecture and domain-driven design, "domain" refers to the core layer of business entities and rules, which contains no controllers or HTTP code. Naming this folder `Domains` would suggest that meaning even though each folder also holds its controllers. `Features` is the conventional name for this layout. It also fits technical endpoints like Health, which isn't a business domain.
