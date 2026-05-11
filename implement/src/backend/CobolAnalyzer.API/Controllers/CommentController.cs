using System.Text.RegularExpressions;
using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Engine.Comment;
using Microsoft.AspNetCore.Mvc;

namespace CobolAnalyzer.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommentController : ControllerBase
{
    private static readonly Regex TagPattern = new("^[A-Z0-9-]+$", RegexOptions.Compiled);
    private static readonly Regex ValuePattern = new("^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);

    private readonly CommentInserter _inserter;
    private readonly CommentRemover _remover;

    public CommentController(CommentInserter inserter, CommentRemover remover)
    {
        _inserter = inserter;
        _remover = remover;
    }

    [HttpPost("insert")]
    public IActionResult Insert([FromBody] CommentInsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Source))
            return BadRequest(new { error = "source is required" });
        if (request.Insertions is null || request.Insertions.Count == 0)
            return BadRequest(new { error = "insertions is required" });

        foreach (var insertion in request.Insertions)
        {
            if (insertion is null)
                return BadRequest(new { error = "insertion is required" });
            if (insertion.TargetLine <= 0)
                return BadRequest(new { error = "targetLine must be greater than 0" });
            if (string.IsNullOrWhiteSpace(insertion.Tag) || !TagPattern.IsMatch(insertion.Tag))
                return BadRequest(new { error = "tag format is invalid" });
            if (string.IsNullOrWhiteSpace(insertion.Value) || !ValuePattern.IsMatch(insertion.Value))
                return BadRequest(new { error = "value format is invalid" });
        }

        return Ok(_inserter.Insert(request.Source, request.Insertions));
    }

    [HttpPost("preview")]
    public IActionResult Preview([FromBody] CommentRemoveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Source))
            return BadRequest(new { error = "source is required" });
        if (string.IsNullOrWhiteSpace(request.Pattern))
            return BadRequest(new { error = "pattern is required" });

        return Ok(_remover.Preview(request.Source, request.Pattern));
    }

    [HttpPost("remove")]
    public IActionResult Remove([FromBody] CommentRemoveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Source))
            return BadRequest(new { error = "source is required" });
        if (string.IsNullOrWhiteSpace(request.Pattern))
            return BadRequest(new { error = "pattern is required" });

        return Ok(_remover.Remove(request.Source, request.Pattern));
    }
}
