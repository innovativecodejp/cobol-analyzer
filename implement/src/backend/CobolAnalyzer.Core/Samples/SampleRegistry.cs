using System.Text.Json;

namespace CobolAnalyzer.Core.Samples;

/// <summary>
/// <c>samples/registry.json</c> を読み、サンプル名 → 解決済み絶対パスを返すローダ（仕様 §5）。
/// ベースディレクトリ（<c>implement/samples/</c>）は注入可能。未登録名・パス不存在は明示的に扱う。
/// </summary>
public sealed class SampleRegistry
{
    public const string RegistryFileName = "registry.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly List<SampleDefinition> _samples;

    public SampleRegistry(string baseDirectory, IReadOnlyList<SampleDefinition> samples)
    {
        BaseDirectory = Path.GetFullPath(baseDirectory);
        _samples = samples.ToList();
    }

    /// <summary>解決の基準となる <c>samples/</c> の絶対パス。</summary>
    public string BaseDirectory { get; }

    public IReadOnlyList<SampleDefinition> Samples => _samples;

    // ---- 構築 ----

    /// <summary><paramref name="baseDirectory"/>/registry.json を読み込む。</summary>
    public static SampleRegistry Load(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, RegistryFileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"samples registry not found: {path}", path);
        return Parse(baseDirectory, File.ReadAllText(path));
    }

    /// <summary>JSON 文字列から構築する（<paramref name="baseDirectory"/> は解決の基準）。</summary>
    public static SampleRegistry Parse(string baseDirectory, string json)
    {
        RegistryDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<RegistryDocument>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"registry.json のパースに失敗しました: {ex.Message}", ex);
        }

        var samples = doc?.Samples ?? new List<SampleDefinition>();
        return new SampleRegistry(baseDirectory, samples);
    }

    /// <summary>
    /// <paramref name="startDirectory"/>（既定は実行アセンブリ位置）から上位へ辿り、
    /// <c>samples/registry.json</c> を含むディレクトリを探して読み込む。
    /// </summary>
    public static SampleRegistry LoadDefault(string? startDirectory = null)
    {
        var start = startDirectory ?? AppContext.BaseDirectory;
        var baseDir = LocateBaseDirectory(start)
            ?? throw new DirectoryNotFoundException(
                $"samples/{RegistryFileName} が {start} から上位に見つかりませんでした");
        return Load(baseDir);
    }

    /// <summary>上位方向に <c>registry.json</c>（直下）または <c>samples/registry.json</c> を探す。</summary>
    public static string? LocateBaseDirectory(string startDirectory)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, RegistryFileName)))
                return dir.FullName;

            var samples = Path.Combine(dir.FullName, "samples");
            if (File.Exists(Path.Combine(samples, RegistryFileName)))
                return samples;

            dir = dir.Parent;
        }
        return null;
    }

    // ---- 解決 ----

    public bool TryResolve(string name, out ResolvedSample resolved)
    {
        var def = _samples.FirstOrDefault(
            s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (def is null)
        {
            resolved = null!;
            return false;
        }

        var rootPath = Path.GetFullPath(Path.Combine(BaseDirectory, def.Root));
        var cobolDirPath = Path.GetFullPath(Path.Combine(rootPath, def.CobolDir));
        var copybookPaths = def.CopybookDirs
            .Select(d => Path.GetFullPath(Path.Combine(rootPath, d)))
            .ToList();

        resolved = new ResolvedSample
        {
            Definition = def,
            RootPath = rootPath,
            CobolDirPath = cobolDirPath,
            CopybookPaths = copybookPaths,
        };
        return true;
    }

    /// <summary>登録名を解決する。未登録は <see cref="KeyNotFoundException"/>。</summary>
    public ResolvedSample Resolve(string name)
    {
        if (!TryResolve(name, out var resolved))
            throw new KeyNotFoundException($"sample not registered: {name}");
        return resolved;
    }

    private sealed class RegistryDocument
    {
        public List<SampleDefinition>? Samples { get; set; }
    }
}
