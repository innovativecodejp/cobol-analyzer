using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Engine.Cfg;
using CobolAnalyzer.Engine.Dfg;
using CobolAnalyzer.Engine.Metrics;
using CobolAnalyzer.Engine.Metrics.Calculators;

namespace CobolAnalyzer.Engine.Project;

public class ProjectAnalyzer : IProjectAnalyzer
{
    private readonly IProjectSourceParser _parser;
    private readonly CfgBuilder _cfgBuilder;
    private readonly DfgBuilder _dfgBuilder;
    private readonly MdiCalculator _mdiCalculator;
    private readonly CallGraphBuilder _callGraphBuilder;
    private readonly MigrationRankingBuilder _rankingBuilder;

    public ProjectAnalyzer(
        IProjectSourceParser parser,
        CfgBuilder cfgBuilder,
        DfgBuilder dfgBuilder,
        MdiCalculator mdiCalculator,
        CallGraphBuilder callGraphBuilder,
        MigrationRankingBuilder rankingBuilder)
    {
        _parser = parser;
        _cfgBuilder = cfgBuilder;
        _dfgBuilder = dfgBuilder;
        _mdiCalculator = mdiCalculator;
        _callGraphBuilder = callGraphBuilder;
        _rankingBuilder = rankingBuilder;
    }

    public ProjectAnalyzeResult Analyze(IReadOnlyList<CobolSource> sources)
    {
        var programs = new List<AnalyzeResult>();
        var projectErrors = new List<string>();
        var fileNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lineCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var paragraphCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            try
            {
                var result = AnalyzeSingle(source.Source);
                programs.Add(result);

                if (result.Ast is not ProgramNode programNode)
                    continue;

                var programName = NormalizeProgramName(programNode.Name);
                if (programName.Length == 0)
                    continue;

                fileNames[programName] = source.FileName;
                lineCounts[programName] = CountLines(source.Source);
                paragraphCounts[programName] = MigrationRankingBuilder.CountParagraphNodes(programNode);
            }
            catch (Exception ex)
            {
                projectErrors.Add($"{source.FileName}: {ex.Message}");
                programs.Add(new AnalyzeResult
                {
                    Errors = new List<ParseError> { new(0, 0, ex.Message) }
                });
            }
        }

        var dependencyGraph = _callGraphBuilder.Build(programs, fileNames);
        var ranking = _rankingBuilder.Build(programs, dependencyGraph, fileNames, lineCounts, paragraphCounts);

        return new ProjectAnalyzeResult
        {
            Programs = programs,
            DependencyGraph = dependencyGraph,
            Ranking = ranking,
            Errors = projectErrors
        };
    }

    private AnalyzeResult AnalyzeSingle(string source)
    {
        var parseResult = _parser.Parse(source);
        if (!parseResult.IsSuccess || parseResult.Ast is not ProgramNode programNode)
        {
            return new AnalyzeResult
            {
                Errors = parseResult.Errors.Count > 0
                    ? parseResult.Errors
                    : new List<ParseError> { new(0, 0, "Program AST was not produced") }
            };
        }

        var cfg = _cfgBuilder.Build(programNode);
        var dfg = _dfgBuilder.Build(programNode);

        var ccPerParagraph = CyclomaticComplexityCalculator.Calculate(cfg);
        var partialMetrics = new MetricsResult
        {
            ProgramName = cfg.ProgramName,
            CyclomaticComplexity = ccPerParagraph.Values.DefaultIfEmpty(1).Max(),
            GoToDensity = GoToDensityCalculator.Calculate(programNode),
            AlterCount = AlterRiskCalculator.Calculate(programNode),
            MaxNestingDepth = NestingDepthCalculator.Calculate(programNode),
            RedefinesDensity = RedefinesDensityCalculator.Calculate(dfg),
            CrossScopeDependencies = CrossScopeDependencyCalculator.Calculate(dfg, cfg),
            CcPerParagraph = ccPerParagraph
        };

        var metrics = new MetricsResult
        {
            ProgramName = partialMetrics.ProgramName,
            CyclomaticComplexity = partialMetrics.CyclomaticComplexity,
            GoToDensity = partialMetrics.GoToDensity,
            AlterCount = partialMetrics.AlterCount,
            MaxNestingDepth = partialMetrics.MaxNestingDepth,
            RedefinesDensity = partialMetrics.RedefinesDensity,
            CrossScopeDependencies = partialMetrics.CrossScopeDependencies,
            CcPerParagraph = partialMetrics.CcPerParagraph,
            Mdi = _mdiCalculator.Calculate(partialMetrics)
        };

        return new AnalyzeResult
        {
            Ast = programNode,
            Cfg = cfg,
            Dfg = dfg,
            Metrics = metrics
        };
    }

    private static int CountLines(string source)
    {
        if (source.Length == 0)
            return 0;

        var normalized = source.Replace("\r\n", "\n").Replace('\r', '\n');
        return normalized.Split('\n').Length;
    }

    private static string NormalizeProgramName(string name)
        => name.Trim().ToUpperInvariant();
}
