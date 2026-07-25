namespace CobolAnalyzer.Core.Samples;

/// <summary>
/// samples/registry.json の 1 サンプル定義（宣言的）。パスは <c>samples/</c> からの相対。
/// </summary>
public sealed class SampleDefinition
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? License { get; init; }
    public string? SourceUrl { get; init; }

    /// <summary>submodule で固定したコミットハッシュ（再現性の記録）。</summary>
    public string? PinnedCommit { get; init; }

    /// <summary>サンプルのルート（<c>samples/</c> 直下のディレクトリ名。通常 submodule 名）。</summary>
    public string Root { get; init; } = string.Empty;

    /// <summary>COBOL 本体ディレクトリ（<see cref="Root"/> からの相対）。</summary>
    public string CobolDir { get; init; } = string.Empty;

    /// <summary>COBOL ファイルの glob（複数可、拡張子の大小差を吸収）。</summary>
    public IReadOnlyList<string> CobolGlobs { get; init; } = Array.Empty<string>();

    /// <summary>コピーブック検索ディレクトリ（<see cref="Root"/> からの相対、複数可）。</summary>
    public IReadOnlyList<string> CopybookDirs { get; init; } = Array.Empty<string>();
}
