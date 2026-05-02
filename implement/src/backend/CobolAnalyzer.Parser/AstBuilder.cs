using Antlr4.Runtime;
using CobolAnalyzer.Core.Ast;
using CobolAnalyzer.Parser.Generated;
using static CobolAnalyzer.Parser.Generated.Cobol85Parser;

namespace CobolAnalyzer.Parser;

public class AstBuilder
{
    public ProgramNode Build(StartRuleContext ctx)
    {
        var unit = ctx.compilationUnit().programUnit(0);
        var program = new ProgramNode { Location = GetLocation(ctx) };

        var id = unit.identificationDivision();
        if (id != null) program.Children.Add(BuildIdentificationDivision(id));

        var env = unit.environmentDivision();
        if (env != null) program.Children.Add(BuildEnvironmentDivision(env));

        var data = unit.dataDivision();
        if (data != null) program.Children.Add(BuildDataDivision(data));

        var proc = unit.procedureDivision();
        if (proc != null) program.Children.Add(BuildProcedureDivision(proc));

        return program;
    }

    // --- Divisions ---

    private DivisionNode BuildIdentificationDivision(IdentificationDivisionContext ctx)
        => new() { Name = "IDENTIFICATION DIVISION", Location = GetLocation(ctx) };

    private DivisionNode BuildEnvironmentDivision(EnvironmentDivisionContext ctx)
        => new() { Name = "ENVIRONMENT DIVISION", Location = GetLocation(ctx) };

    private DivisionNode BuildDataDivision(DataDivisionContext ctx)
    {
        var node = new DivisionNode { Name = "DATA DIVISION", Location = GetLocation(ctx) };
        foreach (var section in ctx.dataDivisionSection())
        {
            var sectionNode = BuildDataSection(section);
            if (sectionNode != null) node.Children.Add(sectionNode);
        }
        return node;
    }

    private SectionNode? BuildDataSection(DataDivisionSectionContext ctx)
    {
        var ws = ctx.workingStorageSection();
        if (ws != null)
        {
            var section = new SectionNode { Name = "WORKING-STORAGE SECTION", Location = GetLocation(ws) };
            foreach (var item in BuildDataHierarchy(ws.dataDescriptionEntry()))
                section.Children.Add(item);
            return section;
        }

        var fs = ctx.fileSection();
        if (fs != null)
        {
            var section = new SectionNode { Name = "FILE SECTION", Location = GetLocation(fs) };
            foreach (var fd in fs.fileDescriptionEntry())
                foreach (var item in BuildDataHierarchy(fd.dataDescriptionEntry()))
                    section.Children.Add(item);
            return section;
        }

        var ls = ctx.linkageSection();
        if (ls != null)
        {
            var section = new SectionNode { Name = "LINKAGE SECTION", Location = GetLocation(ls) };
            foreach (var item in BuildDataHierarchy(ls.dataDescriptionEntry()))
                section.Children.Add(item);
            return section;
        }

        return null;
    }

    private DivisionNode BuildProcedureDivision(ProcedureDivisionContext ctx)
    {
        var node = new DivisionNode { Name = "PROCEDURE DIVISION", Location = GetLocation(ctx) };
        var body = ctx.procedureDivisionBody();
        if (body == null) return node;

        // Paragraphs at the top level (outside named sections)
        var paragraphs = body.paragraphs();
        if (paragraphs != null)
            foreach (var para in paragraphs.paragraph())
                node.Children.Add(BuildParagraph(para));

        // Named sections
        foreach (var section in body.procedureSection())
            node.Children.Add(BuildProcedureSection(section));

        return node;
    }

    private SectionNode BuildProcedureSection(ProcedureSectionContext ctx)
    {
        var name = ctx.procedureSectionHeader()?.sectionName()?.GetText() ?? "";
        var section = new SectionNode { Name = name, Location = GetLocation(ctx) };
        var paragraphs = ctx.paragraphs();
        if (paragraphs != null)
            foreach (var para in paragraphs.paragraph())
                section.Children.Add(BuildParagraph(para));
        return section;
    }

    private ParagraphNode BuildParagraph(ParagraphContext ctx)
    {
        var name = ctx.paragraphName()?.GetText() ?? "";
        var para = new ParagraphNode { Name = name, Location = GetLocation(ctx) };
        foreach (var sentence in ctx.sentence())
            foreach (var stmt in sentence.statement())
                para.Children.Add(BuildStatement(stmt));
        return para;
    }

    // --- Statements ---

    private StatementNode BuildStatement(StatementContext ctx)
    {
        var loc = GetLocation(ctx);

        if (ctx.goToStatement() != null)
            return BuildGoTo(ctx.goToStatement(), loc);

        if (ctx.performStatement() != null)
            return BuildPerform(ctx.performStatement(), loc);

        if (ctx.alterStatement() != null)
            return new StatementNode { StatementType = "ALTER", Location = loc };

        if (ctx.ifStatement() != null)
            return new StatementNode { StatementType = "IF", Location = loc };

        if (ctx.evaluateStatement() != null)
            return new StatementNode { StatementType = "EVALUATE", Location = loc };

        if (ctx.moveStatement() != null)
            return new StatementNode { StatementType = "MOVE", Location = loc };

        if (ctx.computeStatement() != null)
            return new StatementNode { StatementType = "COMPUTE", Location = loc };

        if (ctx.addStatement() != null)
            return new StatementNode { StatementType = "ADD", Location = loc };

        if (ctx.subtractStatement() != null)
            return new StatementNode { StatementType = "SUBTRACT", Location = loc };

        if (ctx.multiplyStatement() != null)
            return new StatementNode { StatementType = "MULTIPLY", Location = loc };

        if (ctx.divideStatement() != null)
            return new StatementNode { StatementType = "DIVIDE", Location = loc };

        if (ctx.callStatement() != null)
            return new StatementNode { StatementType = "CALL", Location = loc };

        if (ctx.stopStatement() != null)
            return new StatementNode { StatementType = "STOP", Location = loc };

        if (ctx.exitStatement() != null)
            return new StatementNode { StatementType = "EXIT", Location = loc };

        if (ctx.displayStatement() != null)
            return new StatementNode { StatementType = "DISPLAY", Location = loc };

        if (ctx.readStatement() != null)
            return BuildRead(ctx.readStatement(), loc);

        if (ctx.writeStatement() != null)
            return BuildWrite(ctx.writeStatement(), loc);

        if (ctx.openStatement() != null)
            return new StatementNode { StatementType = "OPEN", Location = loc };

        if (ctx.closeStatement() != null)
            return new StatementNode { StatementType = "CLOSE", Location = loc };

        return new StatementNode { StatementType = "UNKNOWN", Location = loc };
    }

    private static StatementNode BuildGoTo(GoToStatementContext ctx, SourceLocation loc)
    {
        var simple = ctx.goToStatementSimple();
        return new StatementNode
        {
            StatementType = "GOTO",
            Location = loc,
            PerformFrom = simple?.procedureName()?.GetText()
        };
    }

    private static StatementNode BuildPerform(PerformStatementContext ctx, SourceLocation loc)
    {
        var proc = ctx.performProcedureStatement();
        if (proc != null)
        {
            // PERFORM THRU
            if (proc.THROUGH() != null || proc.THRU() != null)
            {
                return new StatementNode
                {
                    StatementType = "PERFORM_THRU",
                    Location = loc,
                    PerformFrom = proc.procedureName(0)?.GetText(),
                    PerformThru = proc.procedureName(1)?.GetText()
                };
            }

            // PERFORM LOOP (UNTIL/VARYING)
            var pt = proc.performType();
            if (pt?.performUntil() != null || pt?.performVarying() != null)
                return new StatementNode { StatementType = "PERFORM_LOOP", Location = loc };
        }

        var inline = ctx.performInlineStatement();
        if (inline != null)
        {
            var pt = inline.performType();
            if (pt?.performUntil() != null || pt?.performVarying() != null)
                return new StatementNode { StatementType = "PERFORM_LOOP", Location = loc };
        }

        return new StatementNode { StatementType = "PERFORM", Location = loc };
    }

    private static StatementNode BuildRead(ReadStatementContext ctx, SourceLocation loc)
        => new()
        {
            StatementType = "READ",
            Location = loc,
            IoVerb = "READ",
            FileName = ctx.fileName()?.GetText()
        };

    private static StatementNode BuildWrite(WriteStatementContext ctx, SourceLocation loc)
        => new()
        {
            StatementType = "WRITE",
            Location = loc,
            IoVerb = "WRITE",
            FileName = ctx.recordName()?.GetText()
        };

    // --- Data items ---

    private static List<DataItemNode> BuildDataHierarchy(DataDescriptionEntryContext[] entries)
    {
        var roots = new List<DataItemNode>();
        var stack = new Stack<DataItemNode>();

        foreach (var entry in entries)
        {
            var fmt1 = entry.dataDescriptionEntryFormat1();
            if (fmt1 == null) continue;

            var node = BuildDataItem(fmt1);

            while (stack.Count > 0 && stack.Peek().LevelNumber >= node.LevelNumber)
                stack.Pop();

            if (stack.Count == 0)
                roots.Add(node);
            else
                stack.Peek().Children.Add(node);

            stack.Push(node);
        }

        return roots;
    }

    private static DataItemNode BuildDataItem(DataDescriptionEntryFormat1Context ctx)
    {
        var levelToken = ctx.INTEGERLITERAL() ?? ctx.LEVEL_NUMBER_77();
        int levelNumber = int.TryParse(levelToken?.GetText(), out var lv) ? lv : 0;

        var name = ctx.dataName()?.GetText()
                   ?? (ctx.FILLER() != null ? "FILLER" : "FILLER");

        var picCtx = ctx.dataPictureClause().FirstOrDefault();
        var picture = picCtx?.pictureString()?.GetText();

        var redefCtx = ctx.dataRedefinesClause().FirstOrDefault();
        var redefinesTarget = redefCtx?.dataName()?.GetText();

        return new DataItemNode
        {
            LevelNumber = levelNumber,
            Name = name,
            Picture = picture,
            RedefinesTarget = redefinesTarget,
            Location = GetLocation(ctx)
        };
    }

    // --- Helpers ---

    private static SourceLocation GetLocation(ParserRuleContext ctx)
    {
        var start = ctx.Start;
        var stop = ctx.Stop ?? ctx.Start;
        return new SourceLocation(
            start?.Line ?? 0,
            start?.Column ?? 0,
            stop?.Line ?? 0,
            (stop?.Column ?? 0) + (stop?.Text?.Length ?? 0)
        );
    }
}
