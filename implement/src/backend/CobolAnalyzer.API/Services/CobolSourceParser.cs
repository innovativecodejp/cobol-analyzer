using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Engine.Project;
using CobolAnalyzer.Parser;

namespace CobolAnalyzer.API.Services;

public class CobolSourceParser : IProjectSourceParser
{
    private readonly CobolParserFacade _parser;

    public CobolSourceParser(CobolParserFacade parser)
    {
        _parser = parser;
    }

    public ParseResult Parse(string source) => _parser.Parse(source);
}
