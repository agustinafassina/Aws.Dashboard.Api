namespace Aws.Models.Configuration;

public class CostOptions
{
    public const string SectionName = "Cost";
    public string ProjectTagKey { get; set; } = "Project";
    public string Metric { get; set; } = "UnblendedCost";
}
