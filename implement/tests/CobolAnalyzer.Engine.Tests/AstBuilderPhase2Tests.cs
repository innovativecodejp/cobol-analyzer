using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Parser;

namespace CobolAnalyzer.Engine.Tests;

public class AstBuilderPhase2Tests
{
    private static ProgramNode BuildAst(string source)
    {
        var result = new CobolParserFacade().Parse(source);
        Assert.True(result.IsSuccess, string.Join(", ", result.Errors.Select(e => e.Message)));
        return Assert.IsType<ProgramNode>(result.Ast);
    }

    [Fact]
    public void Build_PerformSingle_StatementTypeIsPerform()
    {
        var source = @"       IDENTIFICATION DIVISION.
       PROGRAM-ID. MYPROG.
       PROCEDURE DIVISION.
       MAIN-PARA.
           PERFORM WORK-PARA.
           STOP RUN.
       WORK-PARA.
           STOP RUN.";

        var ast = BuildAst(source);
        var perform = GetAllStatements(ast).FirstOrDefault(s => s.StatementType == "PERFORM");

        Assert.NotNull(perform);
        Assert.Equal("WORK-PARA", perform.PerformFrom);
        Assert.Null(perform.PerformThru);
    }

    [Fact]
    public void Build_IfBranchStatements_AlsoInChildren()
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

        var ast = BuildAst(source);
        var ifStatement = GetAllStatements(ast).First(s => s.StatementType == "IF");

        Assert.NotEmpty(ifStatement.TrueStatements);
        Assert.NotEmpty(ifStatement.FalseStatements);
        Assert.Contains(ifStatement.Children, child => ReferenceEquals(child, ifStatement.TrueStatements[0]));
        Assert.Contains(ifStatement.Children, child => ReferenceEquals(child, ifStatement.FalseStatements[0]));
    }

    private static IEnumerable<StatementNode> GetAllStatements(AstNode node)
    {
        foreach (var child in node.Children)
        {
            if (child is StatementNode stmt)
                yield return stmt;
            foreach (var nested in GetAllStatements(child))
                yield return nested;
        }
    }
}
