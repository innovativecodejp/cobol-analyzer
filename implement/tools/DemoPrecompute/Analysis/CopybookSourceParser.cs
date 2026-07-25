using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Engine.Project;
using CobolAnalyzer.Parser;

namespace DemoPrecompute.Analysis;

/// <summary>
/// コピーブック検索パスを渡した <see cref="CobolParserFacade"/> を <see cref="IProjectSourceParser"/>
/// として公開する（API の <c>CobolSourceParser</c> 相当。ツールは API プロジェクトを参照しない）。
/// </summary>
internal sealed class CopybookSourceParser : IProjectSourceParser
{
    private readonly CobolParserFacade _facade;

    public CopybookSourceParser(IReadOnlyList<string> copybookPaths)
    {
        _facade = new CobolParserFacade(new CobolPreprocessorOptions
        {
            CopybookPaths = copybookPaths.ToArray()
        });
    }

    public ParseResult Parse(string source) => _facade.Parse(source);
}
