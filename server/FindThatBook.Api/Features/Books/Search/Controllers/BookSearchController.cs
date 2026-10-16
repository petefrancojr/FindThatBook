using System.ComponentModel.DataAnnotations;
using FindThatBook.Api.Features.Books.Search.Services;
using FindThatBook.Api.Features.Books.Search.Models;
using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.Api.Features.Books.Search.Controllers;

[ApiController]
[Route("api/books")]
public class BookSearchController(IBookSearchService bookSearchService) : ControllerBase
{
    private const int MaxQueryLength = 200;

    /// <summary>Finds the books a messy search term most likely refers to.</summary>
    [HttpGet("search")]
    [ProducesResponseType<BookSearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<BookSearchResponse>> Search(
        [FromQuery,
         Required(ErrorMessage = "Enter a search term."),
         StringLength(MaxQueryLength, ErrorMessage = "Search terms can be at most {1} characters.")]
        string q,
        CancellationToken cancellationToken)
    {
        var response = await bookSearchService.SearchAsync(q.Trim(), cancellationToken);
        return Ok(response);
    }
}
