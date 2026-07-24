namespace CobolAnalyzer.Parser;

/// <summary>
/// <see cref="CobolPreprocessor"/> の設定。COPY 解決に使う検索パスと拡張子候補、入れ子展開の深さ上限。
/// 既定（未指定）でも動作し、その場合 COPY は未解決警告扱いになる（仕様 §5）。
/// </summary>
public sealed class CobolPreprocessorOptions
{
    /// <summary>コピーブックを探す検索パス（複数可）。既定は空。</summary>
    public IReadOnlyList<string> CopybookPaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// メンバ名に付与して試す拡張子候補（順に試す）。空文字はメンバ名そのまま（拡張子なし）を意味する。
    /// </summary>
    public IReadOnlyList<string> CopybookExtensions { get; init; } =
        new[] { ".cpy", ".CPY", ".cbl", ".CBL", "" };

    /// <summary>入れ子 COPY 展開の深さ上限（best-effort）。既定 10。</summary>
    public int MaxCopyDepth { get; init; } = 10;
}
