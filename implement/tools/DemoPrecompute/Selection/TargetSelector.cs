using CobolAnalyzer.Engine.Project;

namespace DemoPrecompute.Selection;

/// <summary>
/// デモ対象集合を仕様 §3 の決定論的規則で選定する。
///   1. ランキング MDI 上位 N 本（既定 N=8）
///   2. バケット代表 各1本（CB* / CO* / CS*）。上位 N に無ければ追加。
///   3. 1・2 の和集合（重複排除、ランク昇順で安定化）。
/// </summary>
internal static class TargetSelector
{
    public static readonly string[] Buckets = { "CB", "CO", "CS" };

    public static TargetSelection Select(MigrationRanking ranking, int topN)
    {
        var entries = ranking.Entries;

        var topEntries = entries.Take(topN).ToList();
        var selectedKeys = new HashSet<string>(
            topEntries.Select(e => e.ProgramName), StringComparer.OrdinalIgnoreCase);

        var bucketAdditions = new List<BucketAddition>();
        foreach (var bucket in Buckets)
        {
            // バケットの代表 = そのバケットで最上位（ランク昇順で先頭）のプログラム。
            var rep = entries.FirstOrDefault(e => InBucket(e.ProgramName, bucket));
            if (rep is null)
            {
                bucketAdditions.Add(new BucketAddition(bucket, null, false));
                continue;
            }

            var alreadyInTopN = selectedKeys.Contains(rep.ProgramName);
            if (!alreadyInTopN)
                selectedKeys.Add(rep.ProgramName);

            bucketAdditions.Add(new BucketAddition(bucket, rep.ProgramName, !alreadyInTopN));
        }

        // 和集合をランク昇順で安定化。
        var selected = entries
            .Where(e => selectedKeys.Contains(e.ProgramName))
            .OrderBy(e => e.Rank)
            .ToList();

        return new TargetSelection(topN, topEntries, bucketAdditions, selected);
    }

    private static bool InBucket(string programName, string bucketPrefix)
        => programName.StartsWith(bucketPrefix, StringComparison.OrdinalIgnoreCase);
}

internal sealed record BucketAddition(string Bucket, string? ProgramName, bool AddedBeyondTopN);

internal sealed record TargetSelection(
    int TopN,
    IReadOnlyList<MigrationRankingEntry> TopEntries,
    IReadOnlyList<BucketAddition> BucketAdditions,
    IReadOnlyList<MigrationRankingEntry> Selected);
