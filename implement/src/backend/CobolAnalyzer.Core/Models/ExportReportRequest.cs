namespace CobolAnalyzer.Core.Models;

public class ExportReportRequest
{
    public string FileName { get; init; } = "program.cbl";
    public string? Source { get; init; }
}
