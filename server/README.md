# Server

The ASP.NET Core API for Find That Book. This page covers running it straight from this folder with .NET installed on your machine, without Docker. To run the whole app in Docker instead, see [Running](../README.md#running) in the main README.

## Requirements

- The [.NET 10 SDK](https://dotnet.microsoft.com/download). Check it with `dotnet --version`, which should print `10.x`.
- A Gemini API key from [Google AI Studio](https://aistudio.google.com/apikey), for searches. The API starts without one, but every search that needs Gemini returns `502`.
- Internet access, because searches call Gemini and Open Library.

## Setting the Gemini key

The API reads the key from the `Gemini:ApiKey` setting. The simplest way to set it is the `Gemini__ApiKey` environment variable (two underscores), in the terminal you start the API from:

```bash
# bash
export Gemini__ApiKey=your-key-here
```

```powershell
# PowerShell
$env:Gemini__ApiKey = "your-key-here"
```

The variable only lasts for that terminal, so set it again in a new one. Don't put the key in `appsettings.json` or `appsettings.Development.json`, because both are committed.

Docker reads the key as `GEMINI_API_KEY` from `.env` and passes it to the API as `Gemini__ApiKey`. Run locally, the API doesn't read `.env` or `GEMINI_API_KEY`, even though the error for a missing key mentions `GEMINI_API_KEY`.

## Starting the API

From this folder:

```bash
dotnet run --project FindThatBook.Api
```

The first run restores the NuGet packages. The API uses the `http` profile in `FindThatBook.Api/Properties/launchSettings.json`. It listens on http://localhost:5080, the port the client's dev server forwards `/api` to, and runs in `Development`. Stop it with `Ctrl+C`.

Check that it's up:

```bash
curl http://localhost:5080/api/health
curl "http://localhost:5080/api/books/search?q=tolkien%20hobbit"
```

In `Development`, the OpenAPI document is at http://localhost:5080/openapi/v1.json. The endpoints are described under [API](../README.md#api) in the main README.

To use another port, pass it in and change `target` in `client/proxy.conf.json` to match:

```bash
dotnet run --project FindThatBook.Api --urls http://localhost:5090
```

To start the client against this API, see the [client README](../client/README.md).

## Running the tests

```bash
dotnet test FindThatBook.Api.Tests
```

The tests don't touch the network: Gemini and Open Library are replaced by fakes, so no key is needed.

A few tests call the real Gemini API instead. They're skipped unless `LIVE_TESTS` is `true` and the key is in `GEMINI_API_KEY`. The tests read that name, not `Gemini__ApiKey`:

```bash
# bash
export LIVE_TESTS=true
export GEMINI_API_KEY=your-key-here
dotnet test FindThatBook.Api.Tests --filter Category=Live
```

```powershell
# PowerShell
$env:LIVE_TESTS = "true"
$env:GEMINI_API_KEY = "your-key-here"
dotnet test FindThatBook.Api.Tests --filter Category=Live
```

One run makes 13 calls to Gemini. On the free tier, several runs in a row can hit the quota (`429 RESOURCE_EXHAUSTED`), which also makes the app's searches fail until it resets. See [Running the tests](../README.md#running-the-tests) in the main README for what the live tests check.

## Settings

`FindThatBook.Api/appsettings.json` holds the other settings. Each one can be overridden with an environment variable, using `__` between the section and the name:

| Setting | Default | Environment variable |
|---|---|---|
| `Gemini:ApiKey` | none | `Gemini__ApiKey` |
| `Gemini:Model` | `gemini-3.5-flash-lite` | `Gemini__Model` |
| `Gemini:TimeoutSeconds` | `10` | `Gemini__TimeoutSeconds` |
| `OpenLibrary:UserAgent` | `FindThatBook/1.0` | `OpenLibrary__UserAgent` |
| `OpenLibrary:TimeoutSeconds` | `10` | `OpenLibrary__TimeoutSeconds` |
