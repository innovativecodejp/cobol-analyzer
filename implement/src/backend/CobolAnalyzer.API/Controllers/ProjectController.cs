using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Engine.Project;
using Microsoft.AspNetCore.Mvc;

namespace CobolAnalyzer.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectController : ControllerBase
{
    private const int MaxSources = 50;
    private readonly IProjectAnalyzer _projectAnalyzer;

    public ProjectController(IProjectAnalyzer projectAnalyzer)
    {
        _projectAnalyzer = projectAnalyzer;
    }

    [HttpPost("analyze")]
    public IActionResult Analyze([FromBody] ProjectAnalyzeRequest? request)
    {
        var validationError = ValidateSources(request?.Sources);
        if (validationError is not null)
            return BadRequest(new { error = validationError });

        return Ok(_projectAnalyzer.Analyze(request!.Sources));
    }

    private static string? ValidateSources(List<CobolSource>? sources)
    {
        if (sources is null || sources.Count == 0)
            return "sources is required";
        if (sources.Count > MaxSources)
            return "sources must be 50 or fewer";

        foreach (var source in sources)
        {
            if (source is null)
                return "source item is required";
            if (string.IsNullOrWhiteSpace(source.FileName))
                return "fileName is required";
            if (string.IsNullOrWhiteSpace(source.Source))
                return "source is required";
        }

        return null;
    }
}
