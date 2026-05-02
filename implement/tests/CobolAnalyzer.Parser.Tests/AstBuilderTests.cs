using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Parser;

namespace CobolAnalyzer.Parser.Tests;

public class AstBuilderTests
{
    private static readonly CobolParserFacade Facade = new();
    private static string TestData(string file)
        => File.ReadAllText(Path.Combine("TestData", file));

    [Fact]
    public void Build_ProcedureDivision_ContainsParagraphs()
    {
        var result = Facade.Parse(TestData("hello.cbl"));

        Assert.True(result.IsSuccess);
        var proc = result.Ast!.Children.OfType<DivisionNode>()
            .First(d => d.Name == "PROCEDURE DIVISION");
        var paragraphs = proc.Children.OfType<ParagraphNode>().ToList();
        Assert.NotEmpty(paragraphs);
        Assert.Contains(paragraphs, p => p.Name == "MAIN-PARA");
    }

    [Fact]
    public void Build_WorkingStorage_ContainsSection()
    {
        var result = Facade.Parse(TestData("hello.cbl"));

        Assert.True(result.IsSuccess);
        var data = result.Ast!.Children.OfType<DivisionNode>()
            .First(d => d.Name == "DATA DIVISION");
        var sections = data.Children.OfType<SectionNode>().ToList();
        Assert.Contains(sections, s => s.Name == "WORKING-STORAGE SECTION");
    }

    [Fact]
    public void Build_GoTo_StatementTypeIsGoto()
    {
        var result = Facade.Parse(TestData("goto-sample.cbl"));

        Assert.True(result.IsSuccess);
        var stmts = GetAllStatements(result.Ast!);
        Assert.Contains(stmts, s => s.StatementType == "GOTO");
    }

    [Fact]
    public void Build_PerformThru_PreservesFromAndThru()
    {
        var result = Facade.Parse(TestData("goto-sample.cbl"));

        Assert.True(result.IsSuccess);
        var stmts = GetAllStatements(result.Ast!);
        var performThru = stmts.FirstOrDefault(s => s.StatementType == "PERFORM_THRU");
        Assert.NotNull(performThru);
        Assert.Equal("CALC-PARA", performThru.PerformFrom);
        Assert.Equal("CALC-END-PARA", performThru.PerformThru);
    }

    [Fact]
    public void Build_DataItem_PreservesLevelAndPicture()
    {
        var result = Facade.Parse(TestData("hello.cbl"));

        Assert.True(result.IsSuccess);
        var items = GetAllDataItems(result.Ast!);
        var wsMessage = items.FirstOrDefault(i => i.Name == "WS-MESSAGE");
        Assert.NotNull(wsMessage);
        Assert.Equal(1, wsMessage.LevelNumber);
        Assert.NotNull(wsMessage.Picture);
    }

    [Fact]
    public void Build_Redefines_PreservesTargetName()
    {
        var result = Facade.Parse(TestData("data-sample.cbl"));

        Assert.True(result.IsSuccess);
        var items = GetAllDataItems(result.Ast!);
        var redefItem = items.FirstOrDefault(i => i.RedefinesTarget != null);
        Assert.NotNull(redefItem);
        Assert.Equal("WS-NUMERIC", redefItem.RedefinesTarget);
    }

    [Fact]
    public void Build_GroupItem_IsGroupTrue()
    {
        var result = Facade.Parse(TestData("data-sample.cbl"));

        Assert.True(result.IsSuccess);
        var items = GetAllDataItems(result.Ast!);
        var wsBuffer = items.FirstOrDefault(i => i.Name == "WS-BUFFER");
        Assert.NotNull(wsBuffer);
        Assert.True(wsBuffer.IsGroup);
    }

    [Fact]
    public void Build_NodeCategory_MatchesExpected()
    {
        var result = Facade.Parse(TestData("hello.cbl"));

        Assert.True(result.IsSuccess);
        var ast = result.Ast!;

        Assert.Equal(NodeCategory.Structure, ast.Category);

        var division = ast.Children.OfType<DivisionNode>().First();
        Assert.Equal(NodeCategory.Structure, division.Category);

        var proc = ast.Children.OfType<DivisionNode>()
            .First(d => d.Name == "PROCEDURE DIVISION");
        var para = proc.Children.OfType<ParagraphNode>().First();
        Assert.Equal(NodeCategory.Unit, para.Category);

        var stmt = para.Children.OfType<StatementNode>().FirstOrDefault();
        if (stmt != null)
            Assert.Equal(NodeCategory.Element, stmt.Category);
    }

    // --- Helpers ---

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

    private static IEnumerable<DataItemNode> GetAllDataItems(AstNode node)
    {
        foreach (var child in node.Children)
        {
            if (child is DataItemNode item)
                yield return item;
            foreach (var nested in GetAllDataItems(child))
                yield return nested;
        }
    }
}
