using CobolAnalyzer.Core.Models;

namespace CobolAnalyzer.Engine.Project;

public interface IProjectSourceParser
{
    ParseResult Parse(string source);
}
