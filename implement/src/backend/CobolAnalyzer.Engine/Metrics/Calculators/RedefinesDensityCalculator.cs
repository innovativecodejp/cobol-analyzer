using CobolAnalyzer.Engine.Dfg;

namespace CobolAnalyzer.Engine.Metrics.Calculators;

public static class RedefinesDensityCalculator
{
    public static double Calculate(DataFlowGraph dfg)
    {
        int nodeCount = dfg.Nodes.Count;
        if (nodeCount == 0) return 0.0;

        int redefinesCount = dfg.Edges.Count(e => e.Kind == DfgEdgeKind.Redefines);
        return (double)redefinesCount / nodeCount;
    }
}
