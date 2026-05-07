using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Engine;
using CobolAnalyzer.Engine.Cfg;
using CobolAnalyzer.Engine.Dfg;
using CobolAnalyzer.Engine.Metrics;
using CobolAnalyzer.Engine.Metrics.Calculators;
using CobolAnalyzer.Parser;
using Microsoft.AspNetCore.Mvc;

namespace CobolAnalyzer.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyzeController : ControllerBase
{
    private readonly CobolParserFacade _parser;
    private readonly CfgBuilder _cfgBuilder;
    private readonly DfgBuilder _dfgBuilder;
    private readonly MdiCalculator _mdiCalculator;

    public AnalyzeController(CobolParserFacade parser, CfgBuilder cfgBuilder,
        DfgBuilder dfgBuilder, MdiCalculator mdiCalculator)
    {
        _parser = parser;
        _cfgBuilder = cfgBuilder;
        _dfgBuilder = dfgBuilder;
        _mdiCalculator = mdiCalculator;
    }

    [HttpPost]
    public IActionResult Post([FromBody] AnalyzeRequest request)
    {
        if (string.IsNullOrEmpty(request?.Source))
            return BadRequest(new { error = "source is required" });

        try
        {
            var parseResult = _parser.Parse(request.Source);
            if (!parseResult.IsSuccess || parseResult.Ast is not ProgramNode programNode)
            {
                return Ok(new AnalyzeResult
                {
                    Errors = parseResult.Errors
                });
            }

            var cfg = _cfgBuilder.Build(programNode);
            var (dfg, _) = _dfgBuilder.Build(programNode);

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

            var mdi = _mdiCalculator.Calculate(partialMetrics);
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
                Mdi = mdi
            };

            return Ok(new AnalyzeResult
            {
                Ast = programNode,
                Cfg = cfg,
                Dfg = dfg,
                Metrics = metrics
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public record AnalyzeRequest(string? Source);
