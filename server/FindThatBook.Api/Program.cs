using FindThatBook.Api.Middleware;
using FindThatBook.Api.Common.OpenApi;
using FindThatBook.Api.Features.Books;
using FindThatBook.Api.Integrations.Gemini;
using FindThatBook.Api.Integrations.OpenLibrary;
using Microsoft.AspNetCore.HttpLogging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddApiDocumentation();
builder.Services.AddErrorHandling();
builder.Services.AddGemini(builder.Configuration);
builder.Services.AddOpenLibrary(builder.Configuration);
builder.Services.AddBooksFeature();
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.RequestQuery
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
    options.CombineLogs = true;
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpLogging();

// The combined image (Dockerfile at the repo root) puts the Angular build in wwwroot, so one service hosts both.
// Elsewhere wwwroot doesn't exist and these do nothing.
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();
