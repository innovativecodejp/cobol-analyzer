namespace CobolAnalyzer.Engine.Metrics;

public enum MdiRisk { Low, Medium, High, Critical }

public class MdiScore
{
    public double Score { get; init; }
    public MdiRisk Risk { get; init; }
    public Dictionary<string, double> WeightedContributions { get; init; } = new();
}
