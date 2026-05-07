namespace CobolAnalyzer.Core.Ast;

public enum PerformKind { OOL, Inline, Times, Until, Varying }

public class PerformDetailsNode : AstNode
{
    public PerformKind Kind { get; init; }
    public string? TimesExpression { get; init; }
    public ConditionNode? UntilCondition { get; init; }
    public PerformDetailsNode() => Category = NodeCategory.Element;
}
