using CobolAnalyzer.Core.Ast;

namespace CobolAnalyzer.Engine.Project;

public class CallGraphBuilder
{
    public ProgramDependencyGraph Build(
        IReadOnlyList<AnalyzeResult> programs,
        IReadOnlyDictionary<string, string> fileNames)
    {
        var successfulPrograms = programs
            .Where(p => p.Ast is not null)
            .Select(p => p.Ast!)
            .ToList();

        var internalProgramNames = successfulPrograms
            .Select(p => NormalizeProgramName(p.Name))
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var metricsByProgram = programs
            .Where(p => p.Ast is not null && p.Metrics is not null)
            .ToDictionary(
                p => NormalizeProgramName(p.Ast!.Name),
                p => p.Metrics!.Mdi,
                StringComparer.OrdinalIgnoreCase);

        var edges = new Dictionary<(string Caller, string Callee), DependencyEdge>();
        var allNodeNames = new HashSet<string>(internalProgramNames, StringComparer.OrdinalIgnoreCase);
        var hasDynamicCall = false;

        foreach (var program in successfulPrograms)
        {
            var caller = NormalizeProgramName(program.Name);
            if (caller.Length == 0)
                continue;

            foreach (var statement in CollectStatements(program))
            {
                if (statement.StatementType != "CALL")
                    continue;

                if (statement.CallTarget is null)
                {
                    hasDynamicCall = true;
                    continue;
                }

                var callee = NormalizeProgramName(statement.CallTarget);
                if (callee.Length == 0)
                    continue;

                allNodeNames.Add(callee);
                var key = (caller, callee);
                if (!edges.TryGetValue(key, out var edge))
                {
                    edge = new DependencyEdge
                    {
                        CallerProgram = caller,
                        CalleeProgram = callee
                    };
                    edges[key] = edge;
                }

                if (statement.Location is not null)
                    edge.CallSites.Add(statement.Location);
            }
        }

        var edgeList = edges.Values
            .OrderBy(e => e.CallerProgram, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.CalleeProgram, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var fanIn = allNodeNames.ToDictionary(name => name, _ => 0, StringComparer.OrdinalIgnoreCase);
        var fanOut = allNodeNames.ToDictionary(name => name, _ => 0, StringComparer.OrdinalIgnoreCase);

        foreach (var edge in edgeList)
        {
            fanOut[edge.CallerProgram] = fanOut.GetValueOrDefault(edge.CallerProgram) + 1;
            fanIn[edge.CalleeProgram] = fanIn.GetValueOrDefault(edge.CalleeProgram) + 1;
        }

        var nodes = allNodeNames
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new DependencyNode
            {
                ProgramName = name,
                FileName = fileNames.GetValueOrDefault(name),
                Mdi = metricsByProgram.GetValueOrDefault(name),
                IsExternal = !internalProgramNames.Contains(name),
                FanIn = fanIn.GetValueOrDefault(name),
                FanOut = fanOut.GetValueOrDefault(name)
            })
            .ToList();

        return new ProgramDependencyGraph
        {
            Nodes = nodes,
            Edges = edgeList,
            HasCycle = HasCycle(edgeList),
            HasDynamicCall = hasDynamicCall
        };
    }

    private static IEnumerable<StatementNode> CollectStatements(ProgramNode program)
    {
        var seen = new HashSet<StatementNode>(ReferenceEqualityComparer<StatementNode>.Instance);
        foreach (var statement in CollectStatementsRecursive(program, seen))
            yield return statement;
    }

    private static IEnumerable<StatementNode> CollectStatementsRecursive(
        AstNode node,
        HashSet<StatementNode> seen)
    {
        if (node is StatementNode statement && seen.Add(statement))
        {
            yield return statement;

            foreach (var child in statement.TrueStatements)
                foreach (var nested in CollectStatementsRecursive(child, seen))
                    yield return nested;

            foreach (var child in statement.FalseStatements)
                foreach (var nested in CollectStatementsRecursive(child, seen))
                    yield return nested;
        }

        foreach (var child in node.Children)
            foreach (var nested in CollectStatementsRecursive(child, seen))
                yield return nested;
    }

    private static bool HasCycle(IReadOnlyList<DependencyEdge> edges)
    {
        var graph = edges
            .GroupBy(e => e.CallerProgram, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.CalleeProgram).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in graph.Keys)
            if (HasCycleFrom(node, graph, visited, inStack))
                return true;

        return false;
    }

    private static bool HasCycleFrom(
        string node,
        IReadOnlyDictionary<string, List<string>> graph,
        HashSet<string> visited,
        HashSet<string> inStack)
    {
        if (inStack.Contains(node))
            return true;
        if (visited.Contains(node))
            return false;

        visited.Add(node);
        inStack.Add(node);

        if (graph.TryGetValue(node, out var callees))
            foreach (var callee in callees)
                if (HasCycleFrom(callee, graph, visited, inStack))
                    return true;

        inStack.Remove(node);
        return false;
    }

    private static string NormalizeProgramName(string name)
        => name.Trim().ToUpperInvariant();
}

internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
    where T : class
{
    public static readonly ReferenceEqualityComparer<T> Instance = new();

    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
