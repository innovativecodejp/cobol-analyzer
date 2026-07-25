using CobolAnalyzer.Core.Samples;

namespace CobolAnalyzer.Core.Tests;

/// <summary>
/// <see cref="SampleRegistry"/> の単体テスト（仕様 §8）。registry.json は実ファイルを読む（ハードコード禁止）。
/// </summary>
public class SampleRegistryTests
{
    private static string SamplesBase()
        => Path.Combine(AppContext.BaseDirectory, "TestData", "samples-base");

    private static SampleRegistry Load()
        => SampleRegistry.Load(SamplesBase());

    [Fact]
    public void Load_ReadsAllSampleDefinitions()
    {
        var registry = Load();
        Assert.Equal(2, registry.Samples.Count);
        Assert.Contains(registry.Samples, s => s.Name == "demo");
    }

    [Fact]
    public void Resolve_RegisteredName_ComposesAbsolutePaths()
    {
        var resolved = Load().Resolve("demo");

        Assert.Equal(Path.Combine(SamplesBase(), "demo-root", "src"), resolved.CobolDirPath);
        Assert.True(resolved.Exists);
        // copybookDirs は Root からの相対で解決され、複数保持される
        Assert.Equal(
            new[]
            {
                Path.Combine(SamplesBase(), "demo-root", "copy"),
                Path.Combine(SamplesBase(), "demo-root", "copy2"),
            },
            resolved.CopybookPaths);
    }

    [Fact]
    public void Resolve_EnumeratesCobolFilesByGlob()
    {
        var resolved = Load().Resolve("demo");
        var files = resolved.EnumerateCobolFiles();
        Assert.Contains(files, f => Path.GetFileName(f) == "HELLO.cbl");
    }

    [Fact]
    public void Resolve_CaseInsensitiveName()
    {
        Assert.True(Load().TryResolve("DEMO", out var resolved));
        Assert.Equal("demo", resolved.Definition.Name);
    }

    [Fact]
    public void Resolve_MissingPath_ExistsIsFalse()
    {
        var resolved = Load().Resolve("missing");
        Assert.False(resolved.Exists);
        Assert.Empty(resolved.EnumerateCobolFiles());
    }

    [Fact]
    public void Resolve_UnregisteredName_IsHandledExplicitly()
    {
        var registry = Load();
        Assert.False(registry.TryResolve("nope", out _));
        Assert.Throws<KeyNotFoundException>(() => registry.Resolve("nope"));
    }

    [Fact]
    public void Load_MissingRegistryFile_Throws()
    {
        var bogus = Path.Combine(AppContext.BaseDirectory, "TestData", "does-not-exist");
        Assert.Throws<FileNotFoundException>(() => SampleRegistry.Load(bogus));
    }

    // ---- 実プロジェクトの registry.json（implement/samples/）を解決できること（§7-2）----

    [Fact]
    public void LoadDefault_ResolvesCardDemoToCopybookPaths()
    {
        // 実行アセンブリ位置から上位へ辿って implement/samples/registry.json を発見する
        var registry = SampleRegistry.LoadDefault(AppContext.BaseDirectory);

        Assert.True(registry.TryResolve("carddemo", out var carddemo));
        Assert.EndsWith(Path.Combine("carddemo", "app", "cbl"), carddemo.CobolDirPath);
        Assert.Single(carddemo.CopybookPaths);
        Assert.EndsWith(Path.Combine("carddemo", "app", "cpy"), carddemo.CopybookPaths[0]);
        Assert.Equal("Apache-2.0", carddemo.Definition.License);
    }
}
