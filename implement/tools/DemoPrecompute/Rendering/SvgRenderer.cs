using System.Globalization;
using System.Text;
using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Engine.Cfg;
using CobolAnalyzer.Engine.Dfg;
using CobolAnalyzer.Engine.Project;

namespace DemoPrecompute.Rendering;

/// <summary>
/// 解析モデルから決定論的に SVG 図を生成する（バックエンドのみ・ブラウザ非依存）。
/// AST は構造概観（Structure/Unit カテゴリ）、CFG/DFG はノード＋エッジ、依存グラフは円環配置。
/// </summary>
internal static class SvgRenderer
{
    private const int NodeH = 26;
    private const int PadX = 16;
    private const int PadY = 16;

    // ---- AST（構造概観：Structure/Unit のみ。Element レベルは可読性のため省略・ログ参照） ----

    public static string RenderAst(ProgramNode program)
    {
        var rows = new List<(int Depth, string Label, string Kind)>();
        Walk(program, 0, rows);
        if (rows.Count == 0)
            rows.Add((0, program.Name, "Program"));

        const int rowH = 30;
        const int indent = 26;
        var maxDepth = rows.Max(r => r.Depth);
        var width = PadX * 2 + 240 + maxDepth * indent;
        var height = PadY * 2 + rows.Count * rowH;

        var sb = new StringBuilder();
        BeginSvg(sb, width, height, $"AST: {program.Name}");
        for (var i = 0; i < rows.Count; i++)
        {
            var (depth, label, kind) = rows[i];
            var x = PadX + depth * indent;
            var y = PadY + i * rowH;
            var fill = kind switch
            {
                "Program" => "#2c3e50",
                "Division" => "#34495e",
                "Section" => "#3d6098",
                "Paragraph" => "#27ae60",
                _ => "#7f8c8d"
            };
            if (depth > 0)
            {
                var px = PadX + (depth - 1) * indent + 6;
                sb.Append($"<path d=\"M{px} {y - rowH + NodeH / 2} V{y + NodeH / 2} H{x}\" ")
                  .Append("fill=\"none\" stroke=\"#c2c8d0\" stroke-width=\"1\"/>\n");
            }
            sb.Append($"<rect x=\"{x}\" y=\"{y}\" width=\"210\" height=\"{NodeH}\" rx=\"4\" ")
              .Append($"fill=\"{fill}\" opacity=\"0.92\"/>\n");
            sb.Append($"<text x=\"{x + 8}\" y=\"{y + 17}\" fill=\"#fff\" font-size=\"12\" ")
              .Append($"font-family=\"monospace\">{Esc(Truncate(label, 30))}</text>\n");
        }
        EndSvg(sb);
        return sb.ToString();
    }

    private static void Walk(AstNode node, int depth, List<(int, string, string)> rows)
    {
        var kind = node.GetType().Name.Replace("Node", "");
        var include = node.Category is NodeCategory.Structure or NodeCategory.Unit;
        if (include)
        {
            var label = node switch
            {
                ProgramNode p => $"Program: {p.Name}",
                ParagraphNode pg => $"¶ {pg.Name}",
                _ => LabelFor(node, kind)
            };
            rows.Add((depth, label, kind));
        }

        var childDepth = include ? depth + 1 : depth;
        foreach (var child in node.Children)
            Walk(child, childDepth, rows);
    }

    private static string LabelFor(AstNode node, string kind)
    {
        var name = node.GetType().GetProperty("Name")?.GetValue(node) as string;
        return string.IsNullOrEmpty(name) ? kind : $"{kind}: {name}";
    }

    // ---- CFG（基本ブロックを縦配置、エッジを右側に描画） ----

    public static string RenderCfg(ControlFlowGraph cfg)
    {
        var blocks = cfg.Blocks;
        const int rowH = 46;
        const int boxW = 260;
        var laneX = PadX;
        var edgeLane = laneX + boxW + 40;
        var width = edgeLane + 60;
        var height = PadY * 2 + Math.Max(1, blocks.Count) * rowH;

        var yById = new Dictionary<string, int>();
        for (var i = 0; i < blocks.Count; i++)
            yById[blocks[i].Id] = PadY + i * rowH;

        var sb = new StringBuilder();
        BeginSvg(sb, width, height, $"CFG: {cfg.ProgramName}");
        Defs(sb);

        foreach (var edge in cfg.Edges)
        {
            if (!yById.TryGetValue(edge.FromBlockId, out var fy) ||
                !yById.TryGetValue(edge.ToBlockId, out var ty))
                continue;
            var y1 = fy + NodeH / 2;
            var y2 = ty + NodeH / 2;
            var stroke = EdgeColor(edge.Kind);
            var dash = edge.IsRecursive ? " stroke-dasharray=\"5,4\"" : "";
            sb.Append($"<path d=\"M{laneX + boxW} {y1} C{edgeLane} {y1}, {edgeLane} {y2}, {laneX + boxW} {y2}\" ")
              .Append($"fill=\"none\" stroke=\"{stroke}\" stroke-width=\"1.5\"{dash} marker-end=\"url(#arrow)\"/>\n");
        }

        foreach (var block in blocks)
        {
            var y = yById[block.Id];
            var label = block.ParagraphName ?? block.Id;
            var sub = $"{block.Statements.Count} stmt";
            sb.Append($"<rect x=\"{laneX}\" y=\"{y}\" width=\"{boxW}\" height=\"{NodeH}\" rx=\"4\" ")
              .Append("fill=\"#eef2f7\" stroke=\"#3d6098\" stroke-width=\"1\"/>\n");
            sb.Append($"<text x=\"{laneX + 8}\" y=\"{y + 12}\" fill=\"#2c3e50\" font-size=\"11\" ")
              .Append($"font-family=\"monospace\">{Esc(Truncate(label, 30))}</text>\n");
            sb.Append($"<text x=\"{laneX + 8}\" y=\"{y + 23}\" fill=\"#7f8c8d\" font-size=\"9\" ")
              .Append($"font-family=\"monospace\">{Esc(sub)}</text>\n");
        }
        EndSvg(sb);
        return sb.ToString();
    }

    private static string EdgeColor(CfgEdgeKind kind) => kind switch
    {
        CfgEdgeKind.GoTo => "#e74c3c",
        CfgEdgeKind.ConditionalTrue => "#27ae60",
        CfgEdgeKind.ConditionalFalse => "#e67e22",
        CfgEdgeKind.PerformCall or CfgEdgeKind.PerformThruCall => "#8e44ad",
        CfgEdgeKind.PerformReturn or CfgEdgeKind.PerformThruReturn => "#b39ddb",
        _ => "#95a5a6"
    };

    // ---- DFG（データ項目を縦配置、エッジを右側に描画） ----

    public static string RenderDfg(DataFlowGraph dfg)
    {
        var nodes = dfg.Nodes;
        const int rowH = 34;
        const int boxW = 260;
        var laneX = PadX;
        var edgeLane = laneX + boxW + 40;
        var width = edgeLane + 60;
        var height = PadY * 2 + Math.Max(1, nodes.Count) * rowH;

        var yById = new Dictionary<string, int>();
        for (var i = 0; i < nodes.Count; i++)
            yById[nodes[i].Id] = PadY + i * rowH;

        var sb = new StringBuilder();
        BeginSvg(sb, width, height, $"DFG: {dfg.ProgramName}");
        Defs(sb);

        foreach (var edge in dfg.Edges)
        {
            if (!yById.TryGetValue(edge.FromId, out var fy) ||
                !yById.TryGetValue(edge.ToId, out var ty))
                continue;
            var y1 = fy + NodeH / 2;
            var y2 = ty + NodeH / 2;
            var stroke = edge.Kind switch
            {
                DfgEdgeKind.Redefines => "#e74c3c",
                DfgEdgeKind.GroupOf => "#8e44ad",
                DfgEdgeKind.Define => "#27ae60",
                _ => "#95a5a6"
            };
            sb.Append($"<path d=\"M{laneX + boxW} {y1} C{edgeLane} {y1}, {edgeLane} {y2}, {laneX + boxW} {y2}\" ")
              .Append($"fill=\"none\" stroke=\"{stroke}\" stroke-width=\"1.2\" marker-end=\"url(#arrow)\"/>\n");
        }

        foreach (var node in nodes)
        {
            var y = yById[node.Id];
            var fill = node.IsGroup ? "#f4ecff" : "#eef2f7";
            var label = $"{node.LevelNumber:D2} {node.Name}";
            var pic = node.Picture is null ? "" : $"PIC {node.Picture}";
            sb.Append($"<rect x=\"{laneX}\" y=\"{y}\" width=\"{boxW}\" height=\"{NodeH}\" rx=\"4\" ")
              .Append($"fill=\"{fill}\" stroke=\"#8e44ad\" stroke-width=\"1\"/>\n");
            sb.Append($"<text x=\"{laneX + 8}\" y=\"{y + 13}\" fill=\"#2c3e50\" font-size=\"11\" ")
              .Append($"font-family=\"monospace\">{Esc(Truncate(label, 30))}</text>\n");
            sb.Append($"<text x=\"{laneX + 8}\" y=\"{y + 25}\" fill=\"#7f8c8d\" font-size=\"9\" ")
              .Append($"font-family=\"monospace\">{Esc(pic)}</text>\n");
        }
        EndSvg(sb);
        return sb.ToString();
    }

    // ---- 依存グラフ（円環配置。ノード色は MDI リスク） ----

    public static string RenderDependencyGraph(ProgramDependencyGraph graph)
    {
        var nodes = graph.Nodes;
        var n = Math.Max(1, nodes.Count);
        var radius = Math.Max(120, n * 16);
        var cx = radius + 120;
        var cy = radius + 60;
        var size = new { W = cx + radius + 120, H = cy + radius + 60 };

        var pos = new Dictionary<string, (double X, double Y)>();
        for (var i = 0; i < nodes.Count; i++)
        {
            var angle = 2 * Math.PI * i / n - Math.PI / 2;
            pos[nodes[i].ProgramName] = (cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));
        }

        var sb = new StringBuilder();
        BeginSvg(sb, size.W, size.H, "Program dependency graph");
        Defs(sb);

        foreach (var edge in graph.Edges)
        {
            if (!pos.TryGetValue(edge.CallerProgram, out var a) ||
                !pos.TryGetValue(edge.CalleeProgram, out var b))
                continue;
            sb.Append($"<line x1=\"{F(a.X)}\" y1=\"{F(a.Y)}\" x2=\"{F(b.X)}\" y2=\"{F(b.Y)}\" ")
              .Append("stroke=\"#b0b8c4\" stroke-width=\"1.2\" marker-end=\"url(#arrow)\"/>\n");
        }

        foreach (var node in nodes)
        {
            var (x, y) = pos[node.ProgramName];
            var fill = node.IsExternal ? "#808080" : RiskColor(node.Mdi?.Risk);
            sb.Append($"<circle cx=\"{F(x)}\" cy=\"{F(y)}\" r=\"9\" fill=\"{fill}\" ")
              .Append("stroke=\"#2c3e50\" stroke-width=\"1\"/>\n");
            var label = node.Mdi is null ? node.ProgramName : $"{node.ProgramName} ({node.Mdi.Score:F0})";
            sb.Append($"<text x=\"{F(x)}\" y=\"{F(y - 13)}\" text-anchor=\"middle\" fill=\"#2c3e50\" ")
              .Append($"font-size=\"10\" font-family=\"monospace\">{Esc(label)}</text>\n");
        }
        EndSvg(sb);
        return sb.ToString();
    }

    private static string RiskColor(CobolAnalyzer.Engine.Metrics.MdiRisk? risk) => risk switch
    {
        CobolAnalyzer.Engine.Metrics.MdiRisk.Critical => "#e74c3c",
        CobolAnalyzer.Engine.Metrics.MdiRisk.High => "#e67e22",
        CobolAnalyzer.Engine.Metrics.MdiRisk.Medium => "#f39c12",
        CobolAnalyzer.Engine.Metrics.MdiRisk.Low => "#27ae60",
        _ => "#95a5a6"
    };

    // ---- SVG 基盤 ----

    private static void BeginSvg(StringBuilder sb, double w, double h, string title)
    {
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {F(w)} {F(h)}\" ")
          .Append($"width=\"{F(w)}\" height=\"{F(h)}\" role=\"img\" aria-label=\"{Esc(title)}\">\n");
        sb.Append($"<title>{Esc(title)}</title>\n");
        sb.Append($"<rect x=\"0\" y=\"0\" width=\"{F(w)}\" height=\"{F(h)}\" fill=\"#ffffff\"/>\n");
    }

    private static void Defs(StringBuilder sb)
    {
        sb.Append("<defs><marker id=\"arrow\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" ")
          .Append("markerWidth=\"7\" markerHeight=\"7\" orient=\"auto-start-reverse\">")
          .Append("<path d=\"M0 0 L10 5 L0 10 z\" fill=\"#7f8c8d\"/></marker></defs>\n");
    }

    private static void EndSvg(StringBuilder sb) => sb.Append("</svg>\n");

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..(max - 1)] + "…";

    private static string Esc(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
        .Replace("\"", "&quot;");
}
