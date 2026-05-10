using CobolAnalyzer.Core.Ast;

namespace CobolAnalyzer.Engine.Dfg;

public class DfgBuilder
{
    public DataFlowGraph Build(ProgramNode ast)
    {
        var nodes = new List<DfgNode>();
        var edges = new List<DfgEdge>();

        // Build nodes from DATA DIVISION
        var dataDiv = ast.Children.OfType<DivisionNode>()
            .FirstOrDefault(d => d.Name == "DATA DIVISION");
        if (dataDiv != null)
        {
            foreach (var section in dataDiv.Children.OfType<SectionNode>())
                foreach (var item in section.Children.OfType<DataItemNode>())
                    CollectDataNodes(item, null, nodes, edges);
        }

        // Build Define/Use edges from PROCEDURE DIVISION
        var procDiv = ast.Children.OfType<DivisionNode>()
            .FirstOrDefault(d => d.Name == "PROCEDURE DIVISION");
        if (procDiv != null)
            CollectStatementEdges(procDiv, nodes, edges);

        var closure = ComputeImpactClosure(nodes, edges);
        return new DataFlowGraph
        {
            ProgramName = ast.Name,
            Nodes = nodes,
            Edges = edges,
            ImpactClosure = closure
        };
    }

    private static void CollectDataNodes(DataItemNode item, string? parentId,
        List<DfgNode> nodes, List<DfgEdge> edges)
    {
        var id = parentId != null ? $"{parentId}.{item.Name}" : item.Name;
        var node = new DfgNode
        {
            Id = id,
            Name = item.Name,
            LevelNumber = item.LevelNumber,
            Picture = item.Picture,
            IsGroup = item.IsGroup
        };
        nodes.Add(node);

        // GroupOf edge: child → parent
        if (parentId != null)
            edges.Add(new DfgEdge { FromId = id, ToId = parentId, Kind = DfgEdgeKind.GroupOf });

        // Redefines edge: this → redefines target
        if (item.RedefinesTarget != null)
        {
            var targetId = parentId != null ? $"{parentId}.{item.RedefinesTarget}" : item.RedefinesTarget;
            edges.Add(new DfgEdge { FromId = id, ToId = targetId, Kind = DfgEdgeKind.Redefines });
        }

        foreach (var child in item.Children.OfType<DataItemNode>())
            CollectDataNodes(child, id, nodes, edges);
    }

    private static void CollectStatementEdges(DivisionNode procDiv,
        List<DfgNode> nodes, List<DfgEdge> edges)
    {
        var nodeIds = new HashSet<string>(nodes.Select(n => n.Id), StringComparer.OrdinalIgnoreCase);

        foreach (var para in CollectParagraphs(procDiv))
            foreach (var stmt in CollectAllStatements(para))
                AddOperandEdges(stmt, nodeIds, edges);
    }

    private static void AddOperandEdges(StatementNode stmt, HashSet<string> nodeIds, List<DfgEdge> edges)
    {
        var stmtRef = stmt.Location != null ? $"Line:{stmt.Location.StartLine}" : null;
        foreach (var op in stmt.Operands)
        {
            // Resolve FQDN: find best matching node id
            var resolvedId = ResolveId(op.DataName, nodeIds);
            if (resolvedId == null) continue;

            if (op.Kind == ReferenceKind.Define)
                edges.Add(new DfgEdge { FromId = resolvedId, ToId = resolvedId, Kind = DfgEdgeKind.Define, StatementRef = stmtRef });
            else
                edges.Add(new DfgEdge { FromId = resolvedId, ToId = resolvedId, Kind = DfgEdgeKind.Use, StatementRef = stmtRef });
        }
    }

    private static string? ResolveId(string name, HashSet<string> nodeIds)
    {
        if (nodeIds.Contains(name)) return name;
        // Try suffix match for FQDN: "GROUP.NAME" → look for any id ending in ".NAME"
        var suffix = "." + name;
        return nodeIds.FirstOrDefault(id =>
            id.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
            id.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<ParagraphNode> CollectParagraphs(DivisionNode procDiv)
    {
        foreach (var child in procDiv.Children)
        {
            if (child is ParagraphNode para) yield return para;
            else if (child is SectionNode section)
                foreach (var p in section.Children.OfType<ParagraphNode>())
                    yield return p;
        }
    }

    private static IEnumerable<StatementNode> CollectAllStatements(ParagraphNode para)
    {
        foreach (var child in para.Children.OfType<StatementNode>())
        {
            yield return child;
            foreach (var nested in child.TrueStatements) yield return nested;
            foreach (var nested in child.FalseStatements) yield return nested;
        }
    }

    private static Dictionary<string, List<string>> ComputeImpactClosure(
        List<DfgNode> nodes, List<DfgEdge> edges)
    {
        var dependencyGraph = nodes.ToDictionary(
            n => n.Id,
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        foreach (var statementGroup in edges
            .Where(e => e.StatementRef != null && e.Kind is DfgEdgeKind.Define or DfgEdgeKind.Use)
            .GroupBy(e => e.StatementRef))
        {
            var usedIds = statementGroup
                .Where(e => e.Kind == DfgEdgeKind.Use)
                .Select(e => e.FromId)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            var definedIds = statementGroup
                .Where(e => e.Kind == DfgEdgeKind.Define)
                .Select(e => e.ToId)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var usedId in usedIds)
            {
                if (!dependencyGraph.TryGetValue(usedId, out var impacts)) continue;
                foreach (var definedId in definedIds)
                    if (!usedId.Equals(definedId, StringComparison.OrdinalIgnoreCase))
                        impacts.Add(definedId);
            }
        }

        foreach (var redefines in edges.Where(e => e.Kind == DfgEdgeKind.Redefines))
        {
            if (dependencyGraph.TryGetValue(redefines.ToId, out var targetImpacts))
                targetImpacts.Add(redefines.FromId);
            if (dependencyGraph.TryGetValue(redefines.FromId, out var redefiningImpacts))
                redefiningImpacts.Add(redefines.ToId);
        }

        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectReachable(node.Id, dependencyGraph, reachable);
            reachable.Remove(node.Id);
            result[node.Id] = reachable.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
        }
        return result;
    }

    private static void CollectReachable(string startId,
        Dictionary<string, HashSet<string>> dependencyGraph,
        HashSet<string> reachable)
    {
        if (!dependencyGraph.TryGetValue(startId, out var nextIds)) return;
        foreach (var nextId in nextIds)
        {
            if (!reachable.Add(nextId)) continue;
            CollectReachable(nextId, dependencyGraph, reachable);
        }
    }
}
