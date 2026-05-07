using System.Text.Json.Serialization;

namespace CobolAnalyzer.Core.Ast;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "nodeType")]
[JsonDerivedType(typeof(ProgramNode), "Program")]
[JsonDerivedType(typeof(DivisionNode), "Division")]
[JsonDerivedType(typeof(SectionNode), "Section")]
[JsonDerivedType(typeof(ParagraphNode), "Paragraph")]
[JsonDerivedType(typeof(StatementNode), "Statement")]
[JsonDerivedType(typeof(DataItemNode), "DataItem")]
[JsonDerivedType(typeof(DataReferenceNode), "DataReference")]
[JsonDerivedType(typeof(ConditionNode), "Condition")]
[JsonDerivedType(typeof(PerformDetailsNode), "PerformDetails")]
public abstract class AstNode
{
    // "{NodeType}:{StartLine}:{StartColumn}" — discriminator value + location
    public string Id => Location != null
        ? $"{GetType().Name.Replace("Node", "")}:{Location.StartLine}:{Location.StartColumn}"
        : GetType().Name.Replace("Node", "");

    public NodeCategory Category { get; init; }
    public SourceLocation? Location { get; init; }
    public List<AstNode> Children { get; init; } = new();
}

public record SourceLocation(int StartLine, int StartColumn, int StopLine, int StopColumn);
