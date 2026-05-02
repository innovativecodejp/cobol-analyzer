using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Parser;

namespace CobolAnalyzer.Parser.Tests;

public class ParserTests
{
    private static readonly CobolParserFacade Facade = new();
    private static string TestData(string file)
        => File.ReadAllText(Path.Combine("TestData", file));

    [Fact]
    public void Parse_HelloWorld_ReturnsAstWithDivisions()
    {
        var source = TestData("hello.cbl");
        var result = Facade.Parse(source);

        Assert.True(result.IsSuccess, string.Join(", ", result.Errors.Select(e => e.Message)));
        Assert.NotNull(result.Ast);

        var divisions = result.Ast.Children.OfType<DivisionNode>().ToList();
        Assert.Equal(4, divisions.Count);
        Assert.Contains(divisions, d => d.Name == "IDENTIFICATION DIVISION");
        Assert.Contains(divisions, d => d.Name == "ENVIRONMENT DIVISION");
        Assert.Contains(divisions, d => d.Name == "DATA DIVISION");
        Assert.Contains(divisions, d => d.Name == "PROCEDURE DIVISION");
    }

    [Fact]
    public void Parse_EmptySource_ReturnsError()
    {
        var result = Facade.Parse("");

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
        Assert.Null(result.Ast);
    }

    [Fact]
    public void Parse_SyntaxError_ReturnsErrors()
    {
        var source = TestData("syntax-error.cbl");
        var result = Facade.Parse(source);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_SourceLocation_IsCorrect()
    {
        var source = TestData("hello.cbl");
        var result = Facade.Parse(source);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Ast);
        Assert.NotNull(result.Ast.Location);
        Assert.True(result.Ast.Location.StartLine >= 1);
        Assert.True(result.Ast.Location.StopLine >= result.Ast.Location.StartLine);
    }
}
