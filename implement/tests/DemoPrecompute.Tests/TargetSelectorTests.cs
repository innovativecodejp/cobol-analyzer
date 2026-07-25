using CobolAnalyzer.Engine.Metrics;
using CobolAnalyzer.Engine.Project;
using DemoPrecompute.Selection;

namespace DemoPrecompute.Tests;

/// <summary>
/// 仕様 §3 のデモ対象集合選定（MDI 上位 N ＋ バケット代表 CB*/CO*/CS*）の
/// 決定論・規則を検証する（コーパス全走査はツール実行で担保。ここは選定ロジックの単体）。
/// </summary>
public class TargetSelectorTests
{
    private static MigrationRankingEntry Entry(int rank, string name, double mdi, int fanIn = 0, int fanOut = 0)
        => new()
        {
            Rank = rank,
            ProgramName = name,
            FileName = name + ".cbl",
            Mdi = new MdiScore { Score = mdi, Risk = MdiRisk.Low },
            FanIn = fanIn,
            FanOut = fanOut,
            Strategy = MigrationRankingBuilder.DetermineStrategy(mdi, fanIn, fanOut),
        };

    // ランク昇順に並んだ、CB*/CO*/CS* を含む代表的ランキング。
    private static MigrationRanking SampleRanking() => new()
    {
        Entries = new List<MigrationRankingEntry>
        {
            Entry(1, "COACTUPC", 24.3),
            Entry(2, "CBSTM03B", 19.5),
            Entry(3, "COCRDUPC", 15.3),
            Entry(4, "COTRN02C", 12.4),
            Entry(5, "COCRDLIC", 11.2),
            Entry(6, "COUSR00C", 9.7),
            Entry(7, "COTRN00C", 9.7),
            Entry(8, "COCRDSLC", 9.6),
            Entry(9, "COACTVWC", 9.5),
            Entry(30, "CSUTLDTC", 4.9, fanIn: 2, fanOut: 2),
        },
    };

    [Fact]
    public void Select_TopN_TakesFirstNByRank()
    {
        var selection = TargetSelector.Select(SampleRanking(), topN: 8);

        Assert.Equal(8, selection.TopEntries.Count);
        Assert.Equal(
            new[] { "COACTUPC", "CBSTM03B", "COCRDUPC", "COTRN02C", "COCRDLIC", "COUSR00C", "COTRN00C", "COCRDSLC" },
            selection.TopEntries.Select(e => e.ProgramName));
    }

    [Fact]
    public void Select_BucketRep_AddedBeyondTopN()
    {
        // 上位 8 は CB*/CO* のみ。CS*（CSUTLDTC, rank 30）はバケット代表として追加される。
        var selection = TargetSelector.Select(SampleRanking(), topN: 8);

        Assert.Contains(selection.Selected, e => e.ProgramName == "CSUTLDTC");
        var cs = Assert.Single(selection.BucketAdditions.Where(b => b.Bucket == "CS"));
        Assert.Equal("CSUTLDTC", cs.ProgramName);
        Assert.True(cs.AddedBeyondTopN);
    }

    [Fact]
    public void Select_BucketRep_AlreadyInTopN_NotDuplicated()
    {
        var selection = TargetSelector.Select(SampleRanking(), topN: 8);

        // CB* 代表 CBSTM03B と CO* 代表 COACTUPC は上位 8 に既にいる。
        var cb = Assert.Single(selection.BucketAdditions.Where(b => b.Bucket == "CB"));
        Assert.Equal("CBSTM03B", cb.ProgramName);
        Assert.False(cb.AddedBeyondTopN);

        Assert.Equal(1, selection.Selected.Count(e => e.ProgramName == "CBSTM03B"));
        Assert.Equal(9, selection.Selected.Count); // 上位8 ＋ CS* 1
    }

    [Fact]
    public void Select_Selected_OrderedByRankAscending()
    {
        var selection = TargetSelector.Select(SampleRanking(), topN: 8);

        var ranks = selection.Selected.Select(e => e.Rank).ToList();
        Assert.Equal(ranks.OrderBy(r => r).ToList(), ranks);
    }

    [Fact]
    public void Select_IsDeterministic_SameKeysAndOrder()
    {
        var a = TargetSelector.Select(SampleRanking(), topN: 8);
        var b = TargetSelector.Select(SampleRanking(), topN: 8);

        Assert.Equal(
            a.Selected.Select(e => e.ProgramName),
            b.Selected.Select(e => e.ProgramName));
    }

    [Fact]
    public void Select_MissingBucket_ReportedAsNull_NotAdded()
    {
        var ranking = new MigrationRanking
        {
            Entries = new List<MigrationRankingEntry>
            {
                Entry(1, "CBACT01C", 10.0),
                Entry(2, "COACTUPC", 8.0),
                // CS* 無し
            },
        };

        var selection = TargetSelector.Select(ranking, topN: 8);

        var cs = Assert.Single(selection.BucketAdditions.Where(b => b.Bucket == "CS"));
        Assert.Null(cs.ProgramName);
        Assert.DoesNotContain(selection.Selected, e => e.ProgramName.StartsWith("CS"));
        Assert.Equal(2, selection.Selected.Count);
    }
}
