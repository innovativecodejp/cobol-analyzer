namespace CobolAnalyzer.Core.Samples;

/// <summary>
/// <see cref="SampleDefinition"/> を実在ベースディレクトリに対して解決した結果（絶対パス）。
/// </summary>
public sealed class ResolvedSample
{
    public required SampleDefinition Definition { get; init; }

    /// <summary>サンプルルートの絶対パス（<c>samples/&lt;root&gt;</c>）。</summary>
    public required string RootPath { get; init; }

    /// <summary>COBOL 本体ディレクトリの絶対パス。</summary>
    public required string CobolDirPath { get; init; }

    /// <summary>コピーブック検索パス（絶対）。<see cref="CobolPreprocessorOptions"/> の CopybookPaths にそのまま渡せる。</summary>
    public required IReadOnlyList<string> CopybookPaths { get; init; }

    public IReadOnlyList<string> CobolGlobs => Definition.CobolGlobs;

    /// <summary>解決した COBOL 本体ディレクトリが実在するか（submodule 未取得なら false）。</summary>
    public bool Exists => Directory.Exists(CobolDirPath);

    /// <summary>glob に一致する COBOL ファイルを列挙する（大小差の重複は排除、名前順）。</summary>
    public IReadOnlyList<string> EnumerateCobolFiles()
    {
        if (!Directory.Exists(CobolDirPath)) return Array.Empty<string>();

        var globs = Definition.CobolGlobs.Count > 0 ? Definition.CobolGlobs : new[] { "*.cbl" };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var glob in globs)
            foreach (var file in Directory.EnumerateFiles(CobolDirPath, glob))
                seen.Add(file);

        return seen.OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToList();
    }
}
