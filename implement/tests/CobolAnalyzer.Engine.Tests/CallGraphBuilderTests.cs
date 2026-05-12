using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Engine.Project;

namespace CobolAnalyzer.Engine.Tests;

public class CallGraphBuilderTests
{
    [Fact]
    public void Build_StaticCall_EdgeCreated()
    {
        var graph = BuildGraph(
            Program("PROG-A", Call("PROG-B", 7)),
            Program("PROG-B"));

        var edge = Assert.Single(graph.Edges);
        Assert.Equal("PROG-A", edge.CallerProgram);
        Assert.Equal("PROG-B", edge.CalleeProgram);
        Assert.Single(edge.CallSites);
    }

    [Fact]
    public void Build_DynamicCall_HasDynamicCallTrue()
    {
        var graph = BuildGraph(Program("PROG-A", new StatementNode
        {
            StatementType = "CALL",
            Location = new SourceLocation(8, 12, 8, 20)
        }));

        Assert.True(graph.HasDynamicCall);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public void Build_ExternalProgram_IsExternalTrue()
    {
        var graph = BuildGraph(Program("PROG-A", Call("EXT-PROG", 6)));

        var external = Assert.Single(graph.Nodes, n => n.ProgramName == "EXT-PROG");
        Assert.True(external.IsExternal);
        Assert.Null(external.FileName);
    }

    [Fact]
    public void Build_CircularCall_HasCycleTrue()
    {
        var graph = BuildGraph(
            Program("PROG-A", Call("PROG-B", 6)),
            Program("PROG-B", Call("PROG-A", 6)));

        Assert.True(graph.HasCycle);
    }

    [Fact]
    public void Build_FanInFanOut_Correct()
    {
        var graph = BuildGraph(
            Program("PROG-A", Call("PROG-B", 6), Call("PROG-C", 7)),
            Program("PROG-B", Call("PROG-C", 6)),
            Program("PROG-C"));

        var a = Assert.Single(graph.Nodes, n => n.ProgramName == "PROG-A");
        var b = Assert.Single(graph.Nodes, n => n.ProgramName == "PROG-B");
        var c = Assert.Single(graph.Nodes, n => n.ProgramName == "PROG-C");

        Assert.Equal(0, a.FanIn);
        Assert.Equal(2, a.FanOut);
        Assert.Equal(1, b.FanIn);
        Assert.Equal(1, b.FanOut);
        Assert.Equal(2, c.FanIn);
        Assert.Equal(0, c.FanOut);
    }

    private static ProgramDependencyGraph BuildGraph(params ProgramNode[] programs)
    {
        var results = programs
            .Select(p => new AnalyzeResult { Ast = p })
            .ToList();
        var fileNames = programs.ToDictionary(p => p.Name, p => $"{p.Name}.cbl");

        return new CallGraphBuilder().Build(results, fileNames);
    }

    private static ProgramNode Program(string name, params StatementNode[] statements)
    {
        var paragraph = new ParagraphNode
        {
            Name = "MAIN-PARA"
        };
        paragraph.Children.AddRange(statements);

        return new ProgramNode
        {
            Name = name,
            Children =
            {
                new DivisionNode
                {
                    Name = "PROCEDURE DIVISION",
                    Children =
                    {
                        paragraph
                    }
                }
            }
        };
    }

    private static StatementNode Call(string target, int line)
        => new()
        {
            StatementType = "CALL",
            CallTarget = target,
            Location = new SourceLocation(line, 12, line, 24)
        };
}
