using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Engine.Cfg;
using CobolAnalyzer.Engine.Dfg;
using CobolAnalyzer.Engine.Metrics;

namespace CobolAnalyzer.Engine;

public class AnalyzeResult
{
    public ProgramNode? Ast { get; init; }
    public ControlFlowGraph? Cfg { get; init; }
    public DataFlowGraph? Dfg { get; init; }
    public MetricsResult? Metrics { get; init; }
    public List<ParseError> Errors { get; init; } = new();
    public bool IsSuccess => Errors.Count == 0;
}
