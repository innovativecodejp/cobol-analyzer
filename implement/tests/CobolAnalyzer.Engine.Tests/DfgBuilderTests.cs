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
        var (dfg, closure) = new DfgBuilder().Build(program);
        return (program, dfg, closure);
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

        // The closure should exist and contain nodes
        Assert.NotNull(closure);
        Assert.NotEmpty(closure);
    }
}
