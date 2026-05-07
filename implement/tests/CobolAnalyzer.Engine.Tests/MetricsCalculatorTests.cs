using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Engine.Cfg;
using CobolAnalyzer.Engine.Dfg;
using CobolAnalyzer.Engine.Metrics;
using CobolAnalyzer.Engine.Metrics.Calculators;
using CobolAnalyzer.Parser;

namespace CobolAnalyzer.Engine.Tests;

public class MetricsCalculatorTests
{
    private static ProgramNode ParseProgram(string source)
    {
        var result = new CobolParserFacade().Parse(source);
        Assert.NotNull(result.Ast);
        return Assert.IsType<ProgramNode>(result.Ast);
    }

    private static ControlFlowGraph BuildCfg(ProgramNode p) => new CfgBuilder().Build(p);
    private static DataFlowGraph BuildDfg(ProgramNode p) => new DfgBuilder().Build(p).Graph;

    private const string LinearProgram = @"       IDENTIFICATION DIVISION.
       PROGRAM-ID. MYPROG.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-X PIC 9.
       PROCEDURE DIVISION.
       MAIN-PARA.
           MOVE 1 TO WS-X.
           STOP RUN.";

    private const string OneBranchProgram = @"       IDENTIFICATION DIVISION.
       PROGRAM-ID. MYPROG.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-X PIC 9.
       01 WS-Y PIC 9.
       PROCEDURE DIVISION.
       MAIN-PARA.
           IF WS-X > 0
               MOVE 1 TO WS-Y
           ELSE
               MOVE 2 TO WS-Y
           END-IF.
           STOP RUN.";

    [Fact]
    public void Cc_LinearProgram_IsOne()
    {
        var p = ParseProgram(LinearProgram);
        var cfg = BuildCfg(p);
        var cc = CyclomaticComplexityCalculator.Calculate(cfg);
        Assert.All(cc.Values, v => Assert.Equal(1, v));
    }

    [Fact]
    public void Cc_OneIf_IsTwo()
    {
        var p = ParseProgram(OneBranchProgram);
        var cfg = BuildCfg(p);
        var cc = CyclomaticComplexityCalculator.Calculate(cfg);
        Assert.Contains(cc.Values, v => v == 2);
    }

    [Fact]
    public void Gd_NoGoTo_IsZero()
    {
        var p = ParseProgram(LinearProgram);
        var gd = GoToDensityCalculator.Calculate(p);
        Assert.Equal(0.0, gd);
    }

    [Fact]
    public void Ad_HasAlter_CountIsOne()
    {
        var source = @"       IDENTIFICATION DIVISION.
       PROGRAM-ID. MYPROG.
       PROCEDURE DIVISION.
       MAIN-PARA.
           ALTER WORK-PARA TO PROCEED TO END-PARA.
           STOP RUN.
       WORK-PARA.
           GO TO WORK-PARA.
       END-PARA.
           STOP RUN.";
        var p = ParseProgram(source);
        Assert.Equal(1, AlterRiskCalculator.Calculate(p));
    }

    [Fact]
    public void Nd_NestedIf_CorrectDepth()
    {
        var source = @"       IDENTIFICATION DIVISION.
       PROGRAM-ID. MYPROG.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-X PIC 9.
       01 WS-Y PIC 9.
       01 WS-Z PIC 9.
       PROCEDURE DIVISION.
       MAIN-PARA.
           IF WS-X > 0
               IF WS-Y > 0
                   MOVE 1 TO WS-Z
               END-IF
           END-IF.
           STOP RUN.";
        var p = ParseProgram(source);
        Assert.Equal(2, NestingDepthCalculator.Calculate(p));
    }

    [Fact]
    public void Mdi_AllZeroMetrics_ScoreIsZero()
    {
        var weights = new MdiWeights();
        var calc = new MdiCalculator(weights);
        var raw = new MetricsResult
        {
            ProgramName = "TEST",
            CyclomaticComplexity = 0,
            GoToDensity = 0.0,
            AlterCount = 0,
            MaxNestingDepth = 0,
            RedefinesDensity = 0.0,
            CrossScopeDependencies = 0
        };
        var score = calc.Calculate(raw);
        Assert.Equal(0.0, score.Score, precision: 10);
        Assert.Equal(MdiRisk.Low, score.Risk);
    }

    [Fact]
    public void Mdi_AllSaturated_ScoreIs100()
    {
        var weights = new MdiWeights();
        var calc = new MdiCalculator(weights);
        var raw = new MetricsResult
        {
            ProgramName = "TEST",
            CyclomaticComplexity = (int)weights.CcSaturation,
            GoToDensity = weights.GdSaturation,
            AlterCount = (int)weights.AdSaturation,
            MaxNestingDepth = (int)weights.NdSaturation,
            RedefinesDensity = weights.RdSaturation,
            CrossScopeDependencies = (int)weights.CsSaturation
        };
        var score = calc.Calculate(raw);
        Assert.Equal(100.0, score.Score, precision: 10);
        Assert.Equal(MdiRisk.Critical, score.Risk);
    }

    [Fact]
    public void Mdi_WeightsFromConfig_Applied()
    {
        var defaultWeights = new MdiWeights();
        var customWeights = new MdiWeights { CyclomaticComplexity = 1.0, GoToDensity = 0, AlterRisk = 0, NestingDepth = 0, RedefinesDensity = 0, CrossScopeDependency = 0 };
        var raw = new MetricsResult
        {
            ProgramName = "TEST",
            CyclomaticComplexity = (int)defaultWeights.CcSaturation
        };

        var defaultScore = new MdiCalculator(defaultWeights).Calculate(raw).Score;
        var customScore = new MdiCalculator(customWeights).Calculate(raw).Score;

        Assert.NotEqual(defaultScore, customScore);
        Assert.Equal(100.0, customScore, precision: 10);
    }
}
