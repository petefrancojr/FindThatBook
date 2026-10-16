using System.ComponentModel.DataAnnotations;
using FindThatBook.Api.Features.Books.Services.Search;
using FindThatBook.Api.Features.Books.Models;
using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.Api.Features.Books.Controllers;

[ApiController]
[Route("api/books")]
public class BooksController(IBookSearchService bookSearchService) : ControllerBase
{
    private const int MaxQueryLength = 200;
    private const string QueryErrorMessage = "Enter a search term of 1 to 200 characters.";

    /// <summary>Finds the books a messy search term most likely refers to.</summary>
    [HttpGet("search")]
    [ProducesResponseType<BookSearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<BookSearchResponse>> Search(
        [FromQuery, Required(ErrorMessage = QueryErrorMessage), StringLength(MaxQueryLength, ErrorMessage = QueryErrorMessage)]
        string q,
        CancellationToken cancellationToken)
    {
        var response = await bookSearchService.SearchAsync(q.Trim(), cancellationToken);
        return Ok(response);
    }
}
