# Client

The Angular front end for Find That Book. This page covers running it straight from this folder with Node installed on your machine, without Docker. To run the whole app in Docker instead, see [Running](../README.md#running) in the main README.

## Requirements

- [Node.js](https://nodejs.org/) 24 (the version the Docker setup uses) with npm.
- The API running on http://localhost:5080, for searches to work. See [Starting the API](#starting-the-api).

## Installing

From this folder, install the packages listed in `package-lock.json`:

```bash
npm ci
```

The Angular CLI is installed into `node_modules`, so a global `ng` isn't needed. The commands below use `npx ng`, or the matching `npm` scripts.

## Starting the API

The app calls the API at `/api/...` on its own address. Start it from the repo root with the [.NET 10 SDK](https://dotnet.microsoft.com/download):

```bash
dotnet run --project server/FindThatBook.Api
```

It listens on http://localhost:5080. Searches need a Gemini API key from [Google AI Studio](https://aistudio.google.com/apikey). Set it in the `Gemini__ApiKey` environment variable in the same terminal before `dotnet run`:

```bash
# bash
export Gemini__ApiKey=your-key-here
```

```powershell
# PowerShell
$env:Gemini__ApiKey = "your-key-here"
```

Without a key the API still starts, but searches return `502`. The [server README](../server/README.md) has more on running the API locally.

## Starting the dev server

```bash
npm start
```

Open http://localhost:4200. The page reloads when you change a source file.

In Docker, nginx forwards `/api` requests to the API. Locally, the dev server does it instead, using `proxy.conf.json`, which `angular.json` sets for `ng serve`. It sends them to http://localhost:5080. If the API runs on another port, change `target` there. The proxy is only used by `ng serve`, so Docker's build and nginx don't read it.

## Running the tests

Unit tests run with [Vitest](https://vitest.dev/). They don't need the API:

```bash
npx ng test --watch=false
```

Leave out `--watch=false` to rerun the tests whenever a file changes.

The end-to-end tests in `e2e/` use Playwright. They answer the search API inside the browser, so they don't need the API or a Gemini key, but they need the app running at http://localhost:4200 (`BASE_URL` points them somewhere else). Install Chromium once, then run them while the dev server is up:

```bash
npx playwright install chromium
npx playwright test
```

They open a browser window and slow every action down so you can follow along. Set `HEADLESS=true` to run them without a window at full speed.

## Building

```bash
npm run build
```

The production build goes to `dist/`.

## Regenerating the API client

The services and models in `src/api/generated/` are generated from the API's OpenAPI document by [ng-openapi-gen](https://github.com/cyclosproject/ng-openapi-gen). Don't edit them by hand. `ng-openapi-gen.json` reads the document from `http://api:8080`, the API's address inside Docker. With the API running locally, point it at the local address instead:

```bash
npx ng-openapi-gen --input http://localhost:5080/openapi/v1.json
```

The other settings still come from `ng-openapi-gen.json`. See [Regenerating the API client](../README.md#regenerating-the-api-client) in the main README for what the C# side needs to keep the generated code clean.
