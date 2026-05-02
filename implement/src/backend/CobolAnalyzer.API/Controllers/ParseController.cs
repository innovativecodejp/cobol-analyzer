using Microsoft.AspNetCore.Mvc;
using CobolAnalyzer.Parser;

namespace CobolAnalyzer.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParseController : ControllerBase
{
    private readonly CobolParserFacade _facade;
    public ParseController(CobolParserFacade facade) => _facade = facade;

    [HttpPost]
    public IActionResult Post([FromBody] ParseRequest request)
    {
        if (string.IsNullOrEmpty(request?.Source))
            return BadRequest(new { error = "source is required" });

        var result = _facade.Parse(request.Source);
        return Ok(result);
    }
}

public record ParseRequest(string? Source);
