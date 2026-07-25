namespace DemoPrecompute.Output;

/// <summary>デモ B/C が読むマニフェスト（対象集合・帰属・選定メタ）。</summary>
internal sealed record Manifest(
    CorpusInfo Corpus,
    SelectionInfo Selection,
    IReadOnlyList<ProgramEntry> Programs,
    string MigrationDesign);

internal sealed record CorpusInfo(
    string Name,
    string Description,
    string License,
    string SourceUrl,
    string PinnedCommit);

internal sealed record SelectionInfo(
    int TopN,
    int Count,
    int TotalPrograms);

internal sealed record ProgramEntry(
    int Rank,
    string ProgramName,
    string FileName,
    double Mdi,
    string Risk,
    string Strategy,
    int FanIn,
    int FanOut,
    string Source,
    string Result,
    string AnnotationReport,
    FigurePaths Figures);

internal sealed record FigurePaths(string Ast, string Cfg, string Dfg);
