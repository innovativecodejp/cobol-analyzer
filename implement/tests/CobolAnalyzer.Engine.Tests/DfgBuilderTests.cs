using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Engine.Dfg;
using CobolAnalyzer.Parser;

namespace CobolAnalyzer.Engine.Tests;

public class DfgBuilderTests
{
    private static (ProgramNode program, DataFlowGraph dfg, Dictionary<string, List<string>> closure)
        BuildFromSource(string source)
    {
        var facade = new CobolParserFacade();
        var result = facade.Parse(source);
        Assert.NotNull(result.Ast);
        var program = Assert.IsType<ProgramNode>(result.Ast);
        var dfg = new DfgBuilder().Build(program);
        return (program, dfg, dfg.ImpactClosure);
    }

    private static string ReadTestData(string fileName)
        => File.ReadAllText(Path.Combine("TestData", fileName));

    [Fact]
    public void Build_MoveStatement_DefineAndUseEdges()
    {
        var source = @"       IDENTIFICATION DIVISION.
       PROGRAM-ID. MYPROG.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-A PIC 9.
       01 WS-B PIC 9.
       PROCEDURE DIVISION.
       MAIN-PARA.
           MOVE WS-A TO WS-B.
           STOP RUN.";

        var (_, dfg, _) = BuildFromSource(source);

        Assert.Contains(dfg.Edges, e => e.Kind == DfgEdgeKind.Define);
        Assert.Contains(dfg.Edges, e => e.Kind == DfgEdgeKind.Use);
    }

    [Fact]
    public void Build_DuplicateFillerInGroup_DoesNotThrow()
    {
        // 実 COBOL では同一グループ配下に複数の FILLER を書ける。
        // 修飾 Id が "GROUP.FILLER" で重複しても解析がクラッシュしないこと（回帰）。
        var source = @"       IDENTIFICATION DIVISION.
       PROGRAM-ID. MYPROG.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-GROUP.
          05 FILLER PIC X(3) VALUE 'ABC'.
          05 WS-FIELD PIC 9.
          05 FILLER PIC X(2) VALUE 'DE'.
       PROCEDURE DIVISION.
       MAIN-PARA.
           MOVE 1 TO WS-FIELD.
           STOP RUN.";

        var exception = Record.Exception(() => BuildFromSource(source));

        Assert.Null(exception);
        var (_, dfg, closure) = BuildFromSource(source);
        Assert.Contains(dfg.Nodes, n => n.Name == "FILLER");
        Assert.NotNull(closure);
    }

    [Fact]
    public void Build_Redefines_RedefinesEdge()
    {
        var (_, dfg, _) = BuildFromSource(ReadTestData("data-sample.cbl"));

        Assert.Contains(dfg.Edges, e => e.Kind == DfgEdgeKind.Redefines);
    }

    [Fact]
    public void Build_GroupItem_GroupOfEdges()
    {
        var (_, dfg, _) = BuildFromSource(ReadTestData("data-sample.cbl"));

        Assert.Contains(dfg.Edges, e => e.Kind == DfgEdgeKind.GroupOf);
    }

    [Fact]
    public void Build_ImpactClosure_CorrectReach()
    {
        // A → MOVE A TO B → B used in MOVE B TO C: A impacts C
        var source = @"       IDENTIFICATION DIVISION.
       PROGRAM-ID. MYPROG.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-A PIC 9.
       01 WS-B PIC 9.
       01 WS-C PIC 9.
       PROCEDURE DIVISION.
       MAIN-PARA.
           MOVE WS-A TO WS-B.
           MOVE WS-B TO WS-C.
           STOP RUN.";

        var (_, dfg, closure) = BuildFromSource(source);

        Assert.True(closure.TryGetValue("WS-A", out var impactedByA));
        Assert.Contains("WS-B", impactedByA);
        Assert.Contains("WS-C", impactedByA);
    }

    [Fact]
    public void Build_Redefines_ImpactClosureIncludesOverlay()
    {
        var (_, _, closure) = BuildFromSource(ReadTestData("data-sample.cbl"));

        Assert.True(closure.TryGetValue("WS-BUFFER.WS-NUMERIC", out var impactedByNumeric));
        Assert.Contains("WS-BUFFER.WS-CHAR", impactedByNumeric);
    }

    [Fact]
    public void Build_ProgramName_SetFromProgramId()
    {
        var (_, dfg, _) = BuildFromSource(ReadTestData("data-sample.cbl"));

        Assert.Equal("DATA-SAMPLE", dfg.ProgramName);
    }
}
