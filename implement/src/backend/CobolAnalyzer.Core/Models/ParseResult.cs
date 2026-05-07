using CobolAnalyzer.Core.Ast;

namespace CobolAnalyzer.Core.Models;

public class ParseResult
{
    public ProgramNode? Ast { get; init; }
    public List<ParseError> Errors { get; init; } = new();
    public bool IsSuccess => Errors.Count == 0;
}

public record ParseError(int Line, int Column, string Message);
