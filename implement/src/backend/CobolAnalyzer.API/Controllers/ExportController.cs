using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Engine.Export;
using Microsoft.AspNetCore.Mvc;

namespace CobolAnalyzer.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private const int MaxSources = 50;
    private readonly AnnotationReportGenerator _annotationReportGenerator;
    private readonly MigrationDesignGenerator _migrationDesignGenerator;

    public ExportController(
        AnnotationReportGenerator annotationReportGenerator,
        MigrationDesignGenerator migrationDesignGenerator)
    {
        _annotationReportGenerator = annotationReportGenerator;
        _migrationDesignGenerator = migrationDesignGenerator;
    }

    [HttpPost("annotation-report")]
    public IActionResult AnnotationReport([FromBody] ExportReportRequest? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Source))
            return BadRequest(new { error = "source is required" });

        var fileName = string.IsNullOrWhiteSpace(request.FileName)
            ? "program.cbl"
            : request.FileName;

        var markdown = _annotationReportGenerator.Generate(fileName, request.Source);
        return Content(markdown, "text/markdown; charset=utf-8");
    }

    [HttpPost("migration-design")]
    public IActionResult MigrationDesign([FromBody] ExportDesignRequest? request)
    {
        var validationError = ValidateSources(request?.Sources);
        if (validationError is not null)
            return BadRequest(new { error = validationError });

        var markdown = _migrationDesignGenerator.Generate(request!.Sources);
        return Content(markdown, "text/markdown; charset=utf-8");
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
