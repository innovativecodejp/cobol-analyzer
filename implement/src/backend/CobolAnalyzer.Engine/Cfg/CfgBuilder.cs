using CobolAnalyzer.Core.Ast;

namespace CobolAnalyzer.Engine.Cfg;

public class CfgBuilder
{
    private int _blockIndex;
    private readonly List<BasicBlock> _blocks = new();
    private readonly List<CfgEdge> _edges = new();
    private bool _hasAlter;

    public ControlFlowGraph Build(ProgramNode ast)
    {
        _blockIndex = 0;
        _blocks.Clear();
        _edges.Clear();
        _hasAlter = false;

        var programName = "";
        var procDiv = ast.Children.OfType<DivisionNode>()
            .FirstOrDefault(d => d.Name == "PROCEDURE DIVISION");
        if (procDiv == null)
            return new ControlFlowGraph { ProgramName = programName };

        // Collect paragraphs (from direct children and section children)
        var paragraphs = CollectParagraphs(procDiv);

        // Build a block for each paragraph
        var paragraphBlockMap = new Dictionary<string, BasicBlock>(StringComparer.OrdinalIgnoreCase);
        foreach (var para in paragraphs)
        {
            var block = BuildParagraphBlock(para);
            paragraphBlockMap[para.Name] = block;
        }

        // Build intra-paragraph blocks (split at IF/GOTO)
        foreach (var para in paragraphs)
            BuildIntraParagraphEdges(para, paragraphBlockMap);

        // Build inter-paragraph edges (GOTO, PERFORM)
        BuildInterParagraphEdges(paragraphs, paragraphBlockMap);

        var entryId = _blocks.FirstOrDefault()?.Id ?? "";
        var exitIds = _blocks
            .Where(b => b.Statements.Any(s => s.StatementType is "STOP" or "EXIT"))
            .Select(b => b.Id)
            .ToList();

        var hasRecursion = DetectRecursion(paragraphBlockMap);

        if (hasRecursion)
            MarkRecursiveEdges(paragraphBlockMap);

        return new ControlFlowGraph
        {
            ProgramName = programName,
            Blocks = _blocks,
            Edges = _edges,
            EntryBlockId = entryId,
            ExitBlockIds = exitIds,
            HasAlter = _hasAlter,
            HasRecursion = hasRecursion
        };
    }

    private static List<ParagraphNode> CollectParagraphs(DivisionNode procDiv)
    {
        var result = new List<ParagraphNode>();
        foreach (var child in procDiv.Children)
        {
            if (child is ParagraphNode para)
                result.Add(para);
            else if (child is SectionNode section)
                result.AddRange(section.Children.OfType<ParagraphNode>());
        }
        return result;
    }

    private BasicBlock BuildParagraphBlock(ParagraphNode para)
    {
        var stmts = CollectFlatStatements(para.Children.OfType<StatementNode>().ToList());
        var block = new BasicBlock
        {
            Id = $"{para.Name}:{_blockIndex++}",
            ParagraphName = para.Name,
            Statements = stmts,
            Location = para.Location
        };
        _blocks.Add(block);
        return block;
    }

    // Flatten TrueStatements/FalseStatements are NOT flattened here;
    // the paragraph-level block contains only direct children.
    private static List<StatementNode> CollectFlatStatements(List<StatementNode> stmts)
        => stmts;

    private void BuildIntraParagraphEdges(ParagraphNode para, Dictionary<string, BasicBlock> map)
    {
        var block = map[para.Name];
        var stmts = para.Children.OfType<StatementNode>().ToList();

        for (int i = 0; i < stmts.Count; i++)
        {
            var stmt = stmts[i];
            if (stmt.StatementType == "ALTER")
                _hasAlter = true;

            if (stmt.StatementType == "IF")
            {
                // Create true-branch block and false-branch block as synthetic blocks
                var trueBlock = CreateSyntheticBlock(para.Name, "true", stmt.TrueStatements);
                var falseBlock = stmt.FalseStatements.Count > 0
                    ? CreateSyntheticBlock(para.Name, "false", stmt.FalseStatements)
                    : null;

                // Create merge block from remaining statements after the IF
                var afterStmts = stmts.Skip(i + 1).ToList();
                BasicBlock? mergeBlock = null;
                if (afterStmts.Count > 0)
                    mergeBlock = CreateSyntheticBlock(para.Name, "merge", afterStmts);

                AddEdge(block.Id, trueBlock.Id, CfgEdgeKind.ConditionalTrue);
                if (falseBlock != null)
                {
                    AddEdge(block.Id, falseBlock.Id, CfgEdgeKind.ConditionalFalse);
                    if (mergeBlock != null)
                    {
                        AddEdge(trueBlock.Id, mergeBlock.Id, CfgEdgeKind.FallThrough);
                        AddEdge(falseBlock.Id, mergeBlock.Id, CfgEdgeKind.FallThrough);
                    }
                }
                else
                {
                    // No else: false branch falls through to same target as true
                    var falseTarget = mergeBlock ?? trueBlock;
                    AddEdge(block.Id, falseTarget.Id, CfgEdgeKind.ConditionalFalse);
                    if (mergeBlock != null)
                        AddEdge(trueBlock.Id, mergeBlock.Id, CfgEdgeKind.FallThrough);
                }
                break; // Remaining stmts are in mergeBlock; stop processing this block
            }
        }
    }

    private BasicBlock CreateSyntheticBlock(string paraName, string suffix, List<StatementNode> stmts)
    {
        var block = new BasicBlock
        {
            Id = $"{paraName}:{_blockIndex++}",
            ParagraphName = paraName,
            Statements = stmts
        };
        _blocks.Add(block);
        return block;
    }

    private void BuildInterParagraphEdges(List<ParagraphNode> paragraphs, Dictionary<string, BasicBlock> map)
    {
        for (int pi = 0; pi < paragraphs.Count; pi++)
        {
            var para = paragraphs[pi];
            var block = map[para.Name];
            var stmts = para.Children.OfType<StatementNode>().ToList();
            bool hasTerminator = false;

            foreach (var stmt in stmts)
            {
                switch (stmt.StatementType)
                {
                    case "GOTO":
                        if (stmt.PerformFrom != null && map.TryGetValue(stmt.PerformFrom, out var gotoTarget))
                            AddEdge(block.Id, gotoTarget.Id, CfgEdgeKind.GoTo);
                        hasTerminator = true;
                        break;

                    case "PERFORM":
                        if (stmt.PerformFrom != null && map.TryGetValue(stmt.PerformFrom, out var performTarget))
                        {
                            AddEdge(block.Id, performTarget.Id, CfgEdgeKind.PerformCall);
                            // Return edge: last block of target back to next sequential block
                            var returnTarget = pi + 1 < paragraphs.Count ? map[paragraphs[pi + 1].Name] : null;
                            if (returnTarget != null)
                                AddEdge(performTarget.Id, returnTarget.Id, CfgEdgeKind.PerformReturn);
                        }
                        break;

                    case "PERFORM_THRU":
                        if (stmt.PerformFrom != null && map.TryGetValue(stmt.PerformFrom, out var thruFrom))
                        {
                            AddEdge(block.Id, thruFrom.Id, CfgEdgeKind.PerformThruCall);
                            if (stmt.PerformThru != null && map.TryGetValue(stmt.PerformThru, out var thruEnd))
                            {
                                var returnTarget = pi + 1 < paragraphs.Count ? map[paragraphs[pi + 1].Name] : null;
                                if (returnTarget != null)
                                    AddEdge(thruEnd.Id, returnTarget.Id, CfgEdgeKind.PerformThruReturn);
                            }
                        }
                        break;

                    case "PERFORM_LOOP":
                        if (stmt.PerformFrom != null && map.TryGetValue(stmt.PerformFrom, out var loopTarget))
                        {
                            AddEdge(block.Id, loopTarget.Id, CfgEdgeKind.PerformCall);
                            var returnTarget = pi + 1 < paragraphs.Count ? map[paragraphs[pi + 1].Name] : null;
                            if (returnTarget != null)
                                AddEdge(loopTarget.Id, returnTarget.Id, CfgEdgeKind.PerformReturn);
                        }
                        break;

                    case "STOP":
                    case "EXIT":
                        hasTerminator = true;
                        break;
                }
            }

            // FallThrough to next paragraph if no terminator
            if (!hasTerminator && pi + 1 < paragraphs.Count)
            {
                var nextBlock = map[paragraphs[pi + 1].Name];
                if (!_edges.Any(e => e.FromBlockId == block.Id && e.ToBlockId == nextBlock.Id))
                    AddEdge(block.Id, nextBlock.Id, CfgEdgeKind.FallThrough);
            }
        }
    }

    // Detect cycles in PERFORM call graph using DFS
    private bool DetectRecursion(Dictionary<string, BasicBlock> map)
    {
        var callGraph = BuildPerformCallGraph(map);
        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();

        foreach (var node in callGraph.Keys)
            if (HasCycle(node, callGraph, visited, inStack))
                return true;
        return false;
    }

    private static Dictionary<string, List<string>> BuildPerformCallGraph(Dictionary<string, BasicBlock> map)
    {
        var graph = new Dictionary<string, List<string>>();
        foreach (var (name, block) in map)
        {
            var calls = new List<string>();
            foreach (var stmt in block.Statements)
            {
                if (stmt.StatementType is "PERFORM" or "PERFORM_THRU" or "PERFORM_LOOP"
                    && stmt.PerformFrom != null)
                    calls.Add(stmt.PerformFrom);
            }
            graph[name] = calls;
        }
        return graph;
    }

    private static bool HasCycle(string node, Dictionary<string, List<string>> graph,
        HashSet<string> visited, HashSet<string> inStack)
    {
        if (inStack.Contains(node)) return true;
        if (visited.Contains(node)) return false;
        visited.Add(node);
        inStack.Add(node);
        if (graph.TryGetValue(node, out var neighbors))
            foreach (var neighbor in neighbors)
                if (HasCycle(neighbor, graph, visited, inStack))
                    return true;
        inStack.Remove(node);
        return false;
    }

    private void MarkRecursiveEdges(Dictionary<string, BasicBlock> map)
    {
        var callGraph = BuildPerformCallGraph(map);
        for (int i = 0; i < _edges.Count; i++)
        {
            var edge = _edges[i];
            if (edge.Kind is not (CfgEdgeKind.PerformCall or CfgEdgeKind.PerformThruCall)) continue;
            var fromPara = _blocks.FirstOrDefault(b => b.Id == edge.FromBlockId)?.ParagraphName;
            var toPara = _blocks.FirstOrDefault(b => b.Id == edge.ToBlockId)?.ParagraphName;
            if (fromPara == null || toPara == null) continue;
            // Check if there's a path from toPara back to fromPara
            if (IsReachable(toPara, fromPara, callGraph))
                _edges[i] = new CfgEdge
                {
                    FromBlockId = edge.FromBlockId,
                    ToBlockId = edge.ToBlockId,
                    Kind = edge.Kind,
                    IsRecursive = true
                };
        }
    }

    private static bool IsReachable(string from, string target, Dictionary<string, List<string>> graph)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == target) return true;
            if (!visited.Add(cur)) continue;
            if (graph.TryGetValue(cur, out var neighbors))
                foreach (var n in neighbors)
                    queue.Enqueue(n);
        }
        return false;
    }

    private void AddEdge(string from, string to, CfgEdgeKind kind)
    {
        if (!_edges.Any(e => e.FromBlockId == from && e.ToBlockId == to && e.Kind == kind))
            _edges.Add(new CfgEdge { FromBlockId = from, ToBlockId = to, Kind = kind });
    }
}
