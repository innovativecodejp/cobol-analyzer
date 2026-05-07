namespace CobolAnalyzer.Engine.Metrics.Calculators;

public class MdiCalculator
{
    private readonly MdiWeights _weights;

    public MdiCalculator(MdiWeights weights)
    {
        _weights = weights;
        ValidateWeights();
    }

    public MdiScore Calculate(MetricsResult raw)
    {
        double n(double x, double sat) => sat > 0 ? Math.Min(x / sat, 1.0) : 0.0;

        var w = _weights;
        var contributions = new Dictionary<string, double>
        {
            ["CC"] = w.CyclomaticComplexity * n(raw.CyclomaticComplexity, w.CcSaturation) * 100,
            ["GD"] = w.GoToDensity          * n(raw.GoToDensity,          w.GdSaturation) * 100,
            ["AD"] = w.AlterRisk            * n(raw.AlterCount,           w.AdSaturation) * 100,
            ["ND"] = w.NestingDepth         * n(raw.MaxNestingDepth,      w.NdSaturation) * 100,
            ["RD"] = w.RedefinesDensity     * n(raw.RedefinesDensity,     w.RdSaturation) * 100,
            ["CS"] = w.CrossScopeDependency * n(raw.CrossScopeDependencies, w.CsSaturation) * 100
        };

        double score = contributions.Values.Sum();
        score = Math.Clamp(score, 0.0, 100.0);

        return new MdiScore
        {
            Score = score,
            Risk = ToRisk(score),
            WeightedContributions = contributions
        };
    }

    private static MdiRisk ToRisk(double score) => score switch
    {
        < 25.0 => MdiRisk.Low,
        < 50.0 => MdiRisk.Medium,
        < 75.0 => MdiRisk.High,
        _      => MdiRisk.Critical
    };

    private void ValidateWeights()
    {
        double sum = _weights.CyclomaticComplexity + _weights.GoToDensity + _weights.AlterRisk
                   + _weights.NestingDepth + _weights.RedefinesDensity + _weights.CrossScopeDependency;
        if (Math.Abs(sum - 1.0) > 1e-9)
            Console.Error.WriteLine($"[MdiCalculator] Warning: weight sum is {sum:F4}, expected 1.0");
    }
}
