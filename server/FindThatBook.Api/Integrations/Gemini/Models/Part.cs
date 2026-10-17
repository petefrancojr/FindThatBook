namespace FindThatBook.Api.Integrations.Gemini.Models;

/// <summary>A piece of a message. Only text parts are used here.</summary>
public class Part
{
    public string? Text { get; init; }
}
