using CobolAnalyzer.Core.Models;
using CobolAnalyzer.Parser;

namespace CobolAnalyzer.Parser.Tests;

/// <summary>
/// 前処理器 <see cref="CobolPreprocessor"/> の単体テスト（仕様 §8）。
/// テストデータはすべて実ファイル読み込み（TestData/preprocess/*）。ハードコード禁止。
/// </summary>
public class PreprocessorTests
{
    private static string Fixture(string relative)
        => File.ReadAllText(Path.Combine("TestData", "preprocess", relative));

    private static readonly string CopybookDir = Path.Combine("TestData", "preprocess", "copybooks");

    // ---- §3.1 固定形式の正規化 ----

    [Fact]
    public void FixedForm_StripsSequenceCommentsAndCol73_MergesContinuation()
    {
        var result = new CobolPreprocessor().Process(Fixture("fixed-form.cbl"));

        // col73 以降（識別領域）は無視される
        Assert.DoesNotContain("IDJUNK73", result.Text);
        Assert.DoesNotContain("STOPJUNK", result.Text);
        // col7 の * / コメント行は除去される
        Assert.DoesNotContain("COMMENT (COL7", result.Text);
        // 継続行（col7 '-'）は直前行へ連結される
        Assert.Contains("'HELLOWORLD'", result.Text);
    }

    [Fact]
    public void FixedForm_ParsesSuccessfullyThroughFacade()
    {
        var result = new CobolParserFacade().Parse(Fixture("fixed-form.cbl"));
        Assert.True(result.IsSuccess, string.Join(", ", result.Errors.Select(e => e.Message)));
        Assert.NotNull(result.Ast);
    }

    // ---- §3.2 旧式 IDENTIFICATION 段落の除去 ----

    [Fact]
    public void ObsoleteIdParagraphs_AreRemovedIncludingFreeText()
    {
        var result = new CobolPreprocessor().Process(Fixture("obsolete-id.cbl"));

        Assert.DoesNotContain("AUTHOR", result.Text);
        Assert.DoesNotContain("SOMEBODY", result.Text);         // AUTHOR. 自由記述（同一行）
        Assert.DoesNotContain("MULTILINE", result.Text);        // AUTHOR. 自由記述（次行）
        Assert.DoesNotContain("INSTALLATION", result.Text);
        Assert.DoesNotContain("DATE-WRITTEN", result.Text);
        Assert.DoesNotContain("DATE-COMPILED", result.Text);
        Assert.DoesNotContain("SECURITY", result.Text);
        Assert.DoesNotContain("CONFIDENTIAL", result.Text);     // SECURITY. 自由記述
        // 段落除去後も本体は残る
        Assert.Contains("WS-X", result.Text);
        Assert.Contains("PROGRAM-ID", result.Text);
    }

    [Fact]
    public void ObsoleteIdParagraphs_ParsesSuccessfullyThroughFacade()
    {
        var result = new CobolParserFacade().Parse(Fixture("obsolete-id.cbl"));
        Assert.True(result.IsSuccess, string.Join(", ", result.Errors.Select(e => e.Message)));
    }

    // ---- §3.3 COPY のテキスト展開 ----

    private static CobolPreprocessor WithCopybooks(int maxDepth = 10)
        => new(new CobolPreprocessorOptions { CopybookPaths = new[] { CopybookDir }, MaxCopyDepth = maxDepth });

    [Fact]
    public void Copy_Resolved_ExpandsContent_Unresolved_Warns()
    {
        var result = WithCopybooks().Process(Fixture("copy-main.cbl"));

        // 解決した COPY の内容が展開される
        Assert.Contains("CUST-RECORD", result.Text);
        Assert.Contains("CUST-NAME", result.Text);
        // 未解決の COPY は無害化され、警告に記録される
        Assert.DoesNotContain("MISSINGBOOK", result.Text);
        Assert.Contains(result.Warnings,
            w => w.Kind == ParseWarningKind.UnresolvedCopy && w.Message.Contains("MISSINGBOOK"));
    }

    [Fact]
    public void Copy_Resolved_ParsesSuccessfullyThroughFacade()
    {
        var facade = new CobolParserFacade(
            new CobolPreprocessorOptions { CopybookPaths = new[] { CopybookDir } });
        var result = facade.Parse(Fixture("copy-main.cbl"));

        Assert.True(result.IsSuccess, string.Join(", ", result.Errors.Select(e => e.Message)));
        // 警告があっても IsSuccess に影響しない（§6）
        Assert.Contains(result.Warnings, w => w.Kind == ParseWarningKind.UnresolvedCopy);
    }

    [Fact]
    public void Copy_NoSearchPath_AllUnresolved()
    {
        // 検索パス未指定でも動作し、COPY はすべて未解決警告になる（§5）
        var result = new CobolPreprocessor().Process(Fixture("copy-main.cbl"));

        Assert.DoesNotContain("CUST-RECORD", result.Text);
        Assert.Equal(2, result.Warnings.Count(w => w.Kind == ParseWarningKind.UnresolvedCopy));
    }

    [Fact]
    public void Copy_Nested_OneLevelExpands()
    {
        var result = WithCopybooks().Process(Fixture("copy-nested.cbl"));

        Assert.Contains("OUTER-FLD", result.Text);
        Assert.Contains("INNER-FLD", result.Text); // 入れ子 COPY（一段）が展開される
    }

    [Fact]
    public void Copy_DepthLimit_StopsNestedExpansion()
    {
        var result = WithCopybooks(maxDepth: 1).Process(Fixture("copy-nested.cbl"));

        Assert.Contains("OUTER-FLD", result.Text);      // 深さ 0 は展開
        Assert.DoesNotContain("INNER-FLD", result.Text); // 深さ上限で入れ子は停止
        Assert.Contains(result.Warnings, w => w.Kind == ParseWarningKind.CopyDepthExceeded);
    }

    [Fact]
    public void Copy_Cycle_IsDetectedAndTerminates()
    {
        // CYCA -> CYCB -> CYCA の循環。無限ループせず循環警告を出す。
        var result = WithCopybooks().Process(Fixture("copybooks/CYCA.cpy"));

        Assert.Contains(result.Warnings, w => w.Kind == ParseWarningKind.CopyCycle);
    }

    [Fact]
    public void Copy_Replacing_IsNeutralizedWithWarning()
    {
        var options = new CobolPreprocessorOptions { CopybookPaths = new[] { CopybookDir } };
        // COPY ... REPLACING を含む行を組み立てず、実ファイルから読む代わりに
        // 既存の copy-main を土台に REPLACING 検出を確認する専用フィクスチャを使う。
        var result = new CobolPreprocessor(options).Process(Fixture("copy-replacing.cbl"));

        Assert.Contains(result.Warnings, w => w.Kind == ParseWarningKind.CopyReplacingUnsupported);
        Assert.DoesNotContain("REPLACING", result.Text.ToUpperInvariant());
    }

    // ---- §3.4 EXEC CICS / EXEC SQL の縮約 ----

    [Fact]
    public void Exec_BlocksAreReducedToContinue()
    {
        var result = new CobolPreprocessor().Process(Fixture("exec.cbl"));

        Assert.Contains("CONTINUE", result.Text);
        Assert.DoesNotContain("END-EXEC", result.Text.ToUpperInvariant());
        Assert.DoesNotContain("EXEC CICS", result.Text.ToUpperInvariant());
        Assert.DoesNotContain("EXEC SQL", result.Text.ToUpperInvariant());
        // CICS ブロックと SQL ブロックの 2 件
        Assert.Equal(2, result.Warnings.Count(w => w.Kind == ParseWarningKind.ExecBlockReduced));
    }

    [Fact]
    public void Exec_ParsesSuccessfullyThroughFacade()
    {
        var result = new CobolParserFacade().Parse(Fixture("exec.cbl"));
        Assert.True(result.IsSuccess, string.Join(", ", result.Errors.Select(e => e.Message)));
    }

    // ---- 既存の自由形式データが前処理経由でも成功すること（§7-2）----

    [Theory]
    [InlineData("hello.cbl")]
    [InlineData("data-sample.cbl")]
    [InlineData("goto-sample.cbl")]
    public void FreeForm_ExistingTestData_StillParsesThroughPreprocessor(string file)
    {
        var source = File.ReadAllText(Path.Combine("TestData", file));
        var result = new CobolParserFacade().Parse(source);
        Assert.True(result.IsSuccess, string.Join(", ", result.Errors.Select(e => e.Message)));
    }

    // ---- §3.5 区切りカンマの正規化 ----

    [Fact]
    public void Comma_SeparatorInCallAndString_NormalizedAndParses()
    {
        var text = Fixture("comma-separator.cbl");

        var pre = new CobolPreprocessor().Process(text);
        // 区切りカンマ（カンマ + 直後空白 / 行末）は空白へ
        Assert.Contains("WS-A  WS-B", pre.Text);   // CALL USING WS-A, WS-B
        Assert.DoesNotContain("WS-A,", pre.Text);  // STRING WS-A, （行末カンマ）含め残らない

        var result = new CobolParserFacade().Parse(text);
        Assert.True(result.IsSuccess, string.Join(", ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void Comma_NonSeparatorAndProtected_AreUntouched()
    {
        var pre = new CobolPreprocessor().Process(Fixture("comma-cases.cbl"));

        Assert.Contains("WS-A  WS-B", pre.Text);            // 区切りカンマは正規化
        Assert.Contains("PIC ZZ,ZZ9", pre.Text);           // PIC 挿入文字（直後が Z）は不変
        Assert.Contains("VALUE 1,5", pre.Text);            // 小数点（直後が数字）は不変
        Assert.Contains("'a, b'", pre.Text);               // リテラル内は不変
        Assert.Contains("'Segoe UI,sans-serif'", pre.Text); // リテラル内は不変
        Assert.Contains("== A, B ==", pre.Text);           // 擬似テキスト内は不変
        Assert.Contains("TBL(I,J)", pre.Text);             // 空白を伴わない添字カンマは不変（§9）
    }

    [Fact]
    public void Comma_Normalization_PreservesLineLength()
    {
        // 同一長置換（桁位置保存）：カンマ 1 文字 → 空白 1 文字。
        var text = Fixture("comma-cases.cbl");
        var pre = new CobolPreprocessor().Process(text);

        var srcLine = NormalizeFixedFormFirstDataLine(text);        // "CALL 'X' USING WS-A, WS-B"
        var outLine = pre.Text.Split('\n').First(l => l.Contains("USING"));
        Assert.Equal(srcLine.Length, outLine.Length);
        Assert.DoesNotContain(",", outLine);
    }

    // 参考：固定形式抽出後の 1 行目相当（テスト内でのハードコードを避けるため実ファイルから導出）
    private static string NormalizeFixedFormFirstDataLine(string fixtureText)
        => fixtureText.Replace("\r\n", "\n").Split('\n')[0].Substring(7).TrimEnd();

    [Fact]
    public void Comma_GoldenLiteral_IsNotCorruptedByNormalization_Section7_5()
    {
        // §7-5 非破壊性 golden：'… Segoe UI,sans-serif …' がカンマ正規化で変質しないこと。
        var pre = new CobolPreprocessor().Process(Fixture("comma-cases.cbl"));
        Assert.Contains("'Segoe UI,sans-serif'", pre.Text);
    }

    // ---- §3.1 リテラル行継続（CBSTM03A 型・§9 格上げ）----

    [Fact]
    public void LiteralContinuation_AcrossHyphen_RejoinsAndPreservesInnerComma()
    {
        var text = Fixture("literal-continuation.cbl");

        var pre = new CobolPreprocessor().Process(text);
        // 継続の再開クォートを除去して 1 つのリテラルに再結合（内部のカンマは保護）
        Assert.Contains("'<td style=\"f:12px Segoe UI,sans-serif;\">'", pre.Text);

        var result = new CobolParserFacade().Parse(text);
        Assert.True(result.IsSuccess, string.Join(", ", result.Errors.Select(e => e.Message)));
    }
}
