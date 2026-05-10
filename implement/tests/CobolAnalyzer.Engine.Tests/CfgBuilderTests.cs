using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Engine.Cfg;
using CobolAnalyzer.Parser;

namespace CobolAnalyzer.Engine.Tests;

public class CfgBuilderTests
{
    private static (ProgramNode program, ControlFlowGraph cfg) BuildFromSource(string source)
    {
        var facade = new CobolParserFacade();
        var result = facade.Parse(source);
        Assert.NotNull(result.Ast);
        var program = Assert.IsType<ProgramNode>(result.Ast);
        var cfg = new CfgBuilder().Build(program);
        return (program, cfg);
    }

    private static string ReadTestData(string fileName)
        => File.ReadAllText(Path.Combine("TestData", fileName));

    [Fact]
    public void Build_SimpleSequence_FallThroughEdges()
    {
        var source = @"       IDENTIFICATION DIVISION.
       PROGRAM-ID. MYPROG.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-X PIC 9.
       PROCEDURE DIVISION.
       PARA-A.
           MOVE 1 TO WS-X.
       PARA-B.
           MOVE 2 TO WS-X.
           STOP RUN.";

        var (_, cfg) = BuildFromSource(source);

        Assert.Contains(cfg.Edges, e => e.Kind == CfgEdgeKind.FallThrough);
    }

    [Fact]
    public void Build_IfStatement_TrueFalseEdges()
    {
        var source = @"       IDENTIFICATION DIVISION.
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

        var (_, cfg) = BuildFromSource(source);

        Assert.Contains(cfg.Edges, e => e.Kind == CfgEdgeKind.ConditionalTrue);
        Assert.Contains(cfg.Edges, e => e.Kind == CfgEdgeKind.ConditionalFalse);
    }

    [Fact]
    public void Build_GoTo_GoToEdge()
    {
        var (_, cfg) = BuildFromSource(ReadTestData("goto-sample.cbl"));
        Assert.Contains(cfg.Edges, e => e.Kind == CfgEdgeKind.GoTo);
    }

    [Fact]
    public void Build_PerformOOL_CallAndReturnEdges()
    {
        var source = @"       IDENTIFICATION DIVISION.
       PROGRAM-ID. MYPROG.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-X PIC 9.
       PROCEDURE DIVISION.
       MAIN-PARA.
           PERFORM WORK-PARA.
           STOP RUN.
       WORK-PARA.
           MOVE 1 TO WS-X.";

        var (_, cfg) = BuildFromSource(source);

        Assert.Contains(cfg.Edges, e => e.Kind == CfgEdgeKind.PerformCall);
        Assert.Contains(cfg.Edges, e => e.Kind == CfgEdgeKind.PerformReturn);
    }

    [Fact]
    public void Build_PerformThru_ThruEdges()
    {
        var (_, cfg) = BuildFromSource(ReadTestData("goto-sample.cbl"));

        Assert.Contains(cfg.Edges, e => e.Kind == CfgEdgeKind.PerformThruCall);
        Assert.Contains(cfg.Edges, e => e.Kind == CfgEdgeKind.PerformThruReturn);
    }

    [Fact]
    public void Build_AlterStatement_HasAlterTrue()
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

        var (_, cfg) = BuildFromSource(source);

        Assert.True(cfg.HasAlter);
    }

    [Fact]
    public void Build_RecursivePerform_HasRecursionTrue()
    {
        var source = @"       IDENTIFICATION DIVISION.
       PROGRAM-ID. MYPROG.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-X PIC 9.
       PROCEDURE DIVISION.
       PARA-A.
           PERFORM PARA-B.
           STOP RUN.
       PARA-B.
           PERFORM PARA-A.";

        var (_, cfg) = BuildFromSource(source);

        Assert.True(cfg.HasRecursion);
    }

    [Fact]
    public void Build_EntryAndExit_Correct()
    {
        var (_, cfg) = BuildFromSource(ReadTestData("hello.cbl"));

        Assert.NotEmpty(cfg.EntryBlockId);
        Assert.NotEmpty(cfg.ExitBlockIds);
        // Entry should be the first block
        Assert.Equal(cfg.Blocks.First().Id, cfg.EntryBlockId);
    }

    [Fact]
    public void Build_ProgramName_SetFromProgramId()
    {
        var (_, cfg) = BuildFromSource(ReadTestData("hello.cbl"));

        Assert.Equal("HELLO", cfg.ProgramName);
    }

    [Fact]
    public void Build_IfBranchGoTo_GoToEdgeFromSyntheticBlock()
    {
        var source = @"       IDENTIFICATION DIVISION.
       PROGRAM-ID. MYPROG.
       DATA DIVISION.
       WORKING-STORAGE SECTION.
       01 WS-X PIC 9.
       PROCEDURE DIVISION.
       MAIN-PARA.
           IF WS-X > 0
               GO TO END-PARA
           END-IF.
           STOP RUN.
       END-PARA.
           STOP RUN.";

        var (_, cfg) = BuildFromSource(source);

        // GoTo edge must exist (from the synthetic true-branch block)
        var gotoEdge = Assert.Single(cfg.Edges, e => e.Kind == CfgEdgeKind.GoTo);
        var sourceBlock = cfg.Blocks.First(b => b.Id == gotoEdge.FromBlockId);
        // The source block is the synthetic true-branch block, not the paragraph block
        Assert.Contains(sourceBlock.Statements, s => s.StatementType == "GOTO");
    }
}
