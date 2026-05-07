namespace CobolAnalyzer.Engine.Metrics;

public class MdiWeights
{
    public double CyclomaticComplexity { get; set; } = 0.25;
    public double GoToDensity { get; set; } = 0.20;
    public double AlterRisk { get; set; } = 0.20;
    public double NestingDepth { get; set; } = 0.15;
    public double RedefinesDensity { get; set; } = 0.10;
    public double CrossScopeDependency { get; set; } = 0.10;

    public double CcSaturation { get; set; } = 50.0;
    public double GdSaturation { get; set; } = 0.3;
    public double AdSaturation { get; set; } = 1.0;
    public double NdSaturation { get; set; } = 8.0;
    public double RdSaturation { get; set; } = 0.3;
    public double CsSaturation { get; set; } = 50.0;
}
