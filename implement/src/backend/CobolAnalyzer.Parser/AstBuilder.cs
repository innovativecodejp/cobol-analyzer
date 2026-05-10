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
        var programName = unit.identificationDivision()
            ?.programIdParagraph()
            ?.programName()
            ?.GetText()
            .Trim('\'', '"')
            .ToUpperInvariant() ?? string.Empty;
        var program = new ProgramNode { Name = programName, Location = GetLocation(ctx) };

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

        var paragraphs = body.paragraphs();
        if (paragraphs != null)
            foreach (var para in paragraphs.paragraph())
                node.Children.Add(BuildParagraph(para));

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
            return BuildIf(ctx.ifStatement(), loc);

        if (ctx.evaluateStatement() != null)
            return new StatementNode { StatementType = "EVALUATE", Location = loc };

        if (ctx.moveStatement() != null)
            return BuildMove(ctx.moveStatement(), loc);

        if (ctx.computeStatement() != null)
            return BuildCompute(ctx.computeStatement(), loc);

        if (ctx.addStatement() != null)
            return BuildAdd(ctx.addStatement(), loc);

        if (ctx.subtractStatement() != null)
            return BuildSubtract(ctx.subtractStatement(), loc);

        if (ctx.multiplyStatement() != null)
            return BuildMultiply(ctx.multiplyStatement(), loc);

        if (ctx.divideStatement() != null)
            return BuildDivide(ctx.divideStatement(), loc);

        if (ctx.callStatement() != null)
            return BuildCall(ctx.callStatement(), loc);

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
            if (proc.THROUGH() != null || proc.THRU() != null)
            {
                var details = new PerformDetailsNode { Kind = PerformKind.OOL };
                return new StatementNode
                {
                    StatementType = "PERFORM_THRU",
                    Location = loc,
                    PerformFrom = proc.procedureName(0)?.GetText(),
                    PerformThru = proc.procedureName(1)?.GetText(),
                    PerformDetails = details
                };
            }

            var pt = proc.performType();
            if (pt?.performUntil() != null)
            {
                var cond = pt.performUntil().condition();
                var condNode = cond != null ? new ConditionNode { ConditionText = cond.GetText() } : null;
                var details = new PerformDetailsNode { Kind = PerformKind.Until, UntilCondition = condNode };
                return new StatementNode
                {
                    StatementType = "PERFORM_LOOP",
                    Location = loc,
                    PerformFrom = proc.procedureName(0)?.GetText(),
                    PerformDetails = details
                };
            }
            if (pt?.performVarying() != null)
            {
                var details = new PerformDetailsNode { Kind = PerformKind.Varying };
                return new StatementNode
                {
                    StatementType = "PERFORM_LOOP",
                    Location = loc,
                    PerformFrom = proc.procedureName(0)?.GetText(),
                    PerformDetails = details
                };
            }

            var oolDetails = new PerformDetailsNode { Kind = PerformKind.OOL };
            return new StatementNode
            {
                StatementType = "PERFORM",
                Location = loc,
                PerformFrom = proc.procedureName(0)?.GetText(),
                PerformDetails = oolDetails
            };
        }

        var inline = ctx.performInlineStatement();
        if (inline != null)
        {
            var pt = inline.performType();
            if (pt?.performUntil() != null)
            {
                var cond = pt.performUntil().condition();
                var condNode = cond != null ? new ConditionNode { ConditionText = cond.GetText() } : null;
                var details = new PerformDetailsNode { Kind = PerformKind.Until, UntilCondition = condNode };
                return new StatementNode { StatementType = "PERFORM_LOOP", Location = loc, PerformDetails = details };
            }
            if (pt?.performVarying() != null)
            {
                var details = new PerformDetailsNode { Kind = PerformKind.Varying };
                return new StatementNode { StatementType = "PERFORM_LOOP", Location = loc, PerformDetails = details };
            }
            var inlineDetails = new PerformDetailsNode { Kind = PerformKind.Inline };
            return new StatementNode { StatementType = "PERFORM", Location = loc, PerformDetails = inlineDetails };
        }

        return new StatementNode { StatementType = "PERFORM", Location = loc };
    }

    private StatementNode BuildIf(IfStatementContext ctx, SourceLocation loc)
    {
        var operands = ExtractIdentifiersFromCondition(ctx.condition());
        var trueStmts = ctx.ifThen()?.statement()
            .Select(s => BuildStatement(s)).ToList() ?? new();
        var falseStmts = ctx.ifElse()?.statement()
            .Select(s => BuildStatement(s)).ToList() ?? new();
        var node = new StatementNode
        {
            StatementType = "IF",
            Location = loc,
            Operands = operands,
            TrueStatements = trueStmts,
            FalseStatements = falseStmts
        };
        // Add branch statements to Children so they are visible in the AST tree
        // and reachable by the frontend's LineNodeIndex DFS traversal (N2 navigation)
        foreach (var s in trueStmts.Concat(falseStmts))
            node.Children.Add(s);
        return node;
    }

    private static StatementNode BuildMove(MoveStatementContext ctx, SourceLocation loc)
    {
        var operands = new List<DataReferenceNode>();

        var simple = ctx.moveToStatement();
        if (simple != null)
        {
            // sending area (Use)
            var sending = simple.moveToSendingArea();
            if (sending?.identifier() != null)
                operands.Add(new DataReferenceNode { DataName = sending.identifier().GetText(), Kind = ReferenceKind.Use });

            // receiving areas (Define)
            foreach (var recv in simple.identifier())
                operands.Add(new DataReferenceNode { DataName = recv.GetText(), Kind = ReferenceKind.Define });
        }

        var corr = ctx.moveCorrespondingToStatement();
        if (corr != null)
        {
            if (corr.identifier(0) != null)
                operands.Add(new DataReferenceNode { DataName = corr.identifier(0).GetText(), Kind = ReferenceKind.Use });
            if (corr.identifier(1) != null)
                operands.Add(new DataReferenceNode { DataName = corr.identifier(1).GetText(), Kind = ReferenceKind.Define });
        }

        return new StatementNode { StatementType = "MOVE", Location = loc, Operands = operands };
    }

    private static StatementNode BuildCompute(ComputeStatementContext ctx, SourceLocation loc)
    {
        var operands = new List<DataReferenceNode>();
        // Left-hand side identifiers (Define)
        foreach (var store in ctx.computeStore())
            operands.Add(new DataReferenceNode { DataName = store.identifier().GetText(), Kind = ReferenceKind.Define });
        return new StatementNode { StatementType = "COMPUTE", Location = loc, Operands = operands };
    }

    private static StatementNode BuildAdd(AddStatementContext ctx, SourceLocation loc)
    {
        var operands = new List<DataReferenceNode>();
        var giving = ctx.addToGivingStatement();
        if (giving != null)
        {
            foreach (var f in giving.addFrom())
                operands.Add(new DataReferenceNode { DataName = f.GetText(), Kind = ReferenceKind.Use });
            foreach (var g in giving.addGiving())
                operands.Add(new DataReferenceNode { DataName = g.identifier().GetText(), Kind = ReferenceKind.Define });
        }
        var to = ctx.addToStatement();
        if (to != null)
        {
            foreach (var f in to.addFrom())
                operands.Add(new DataReferenceNode { DataName = f.GetText(), Kind = ReferenceKind.Use });
            foreach (var t in to.addTo())
                operands.Add(new DataReferenceNode { DataName = t.identifier().GetText(), Kind = ReferenceKind.Define });
        }
        return new StatementNode { StatementType = "ADD", Location = loc, Operands = operands };
    }

    private static StatementNode BuildSubtract(SubtractStatementContext ctx, SourceLocation loc)
    {
        var operands = new List<DataReferenceNode>();
        var fromGiving = ctx.subtractFromGivingStatement();
        if (fromGiving != null)
            foreach (var g in fromGiving.subtractGiving())
                operands.Add(new DataReferenceNode { DataName = g.identifier().GetText(), Kind = ReferenceKind.Define });
        var from = ctx.subtractFromStatement();
        if (from != null)
        {
            foreach (var s in from.subtractSubtrahend())
                operands.Add(new DataReferenceNode { DataName = s.GetText(), Kind = ReferenceKind.Use });
            foreach (var t in from.subtractMinuend())
                operands.Add(new DataReferenceNode { DataName = t.identifier().GetText(), Kind = ReferenceKind.Define });
        }
        return new StatementNode { StatementType = "SUBTRACT", Location = loc, Operands = operands };
    }

    private static StatementNode BuildMultiply(MultiplyStatementContext ctx, SourceLocation loc)
    {
        var operands = new List<DataReferenceNode>();
        var giving = ctx.multiplyGiving();
        if (giving != null)
            foreach (var g in giving.multiplyGivingResult())
                operands.Add(new DataReferenceNode { DataName = g.identifier().GetText(), Kind = ReferenceKind.Define });
        var reg = ctx.multiplyRegular();
        if (reg != null)
            foreach (var t in reg.multiplyRegularOperand())
                operands.Add(new DataReferenceNode { DataName = t.identifier().GetText(), Kind = ReferenceKind.Define });
        return new StatementNode { StatementType = "MULTIPLY", Location = loc, Operands = operands };
    }

    private static StatementNode BuildDivide(DivideStatementContext ctx, SourceLocation loc)
    {
        var operands = new List<DataReferenceNode>();
        var intoGiving = ctx.divideIntoGivingStatement();
        if (intoGiving?.divideGivingPhrase() != null)
            foreach (var g in intoGiving.divideGivingPhrase().divideGiving())
                operands.Add(new DataReferenceNode { DataName = g.identifier().GetText(), Kind = ReferenceKind.Define });
        var into = ctx.divideIntoStatement();
        if (into != null)
            foreach (var t in into.divideInto())
                operands.Add(new DataReferenceNode { DataName = t.identifier().GetText(), Kind = ReferenceKind.Define });
        return new StatementNode { StatementType = "DIVIDE", Location = loc, Operands = operands };
    }

    private static StatementNode BuildCall(CallStatementContext ctx, SourceLocation loc)
    {
        // Static CALL: literal (e.g. CALL "SUBPROG") → CallTarget = normalized name
        // Dynamic CALL: identifier → CallTarget = null
        string? callTarget = null;
        var lit = ctx.literal();
        if (lit != null)
        {
            var raw = lit.GetText().Trim('\'', '"');
            callTarget = raw.ToUpperInvariant();
        }
        return new StatementNode { StatementType = "CALL", Location = loc, CallTarget = callTarget };
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

        var valueCtx = ctx.dataValueClause().FirstOrDefault();
        var value = valueCtx?.dataValueInterval()?.FirstOrDefault()?.dataValueIntervalFrom()?.GetText();

        return new DataItemNode
        {
            LevelNumber = levelNumber,
            Name = name,
            Picture = picture,
            RedefinesTarget = redefinesTarget,
            Value = value,
            Location = GetLocation(ctx)
        };
    }

    // --- Helpers ---

    private static List<DataReferenceNode> ExtractIdentifiersFromCondition(ConditionContext? ctx)
    {
        // Condition identifier extraction is best-effort; grammar depth makes full traversal complex.
        // Condition text is preserved in ConditionNode.ConditionText for display purposes.
        return new List<DataReferenceNode>();
    }

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
