namespace Aws.Models.Configuration;

public class InspectorOptions
{
    public const string SectionName = "Inspector";
    public int PageSize { get; set; } = 100;
    public int MaxTotalFindings { get; set; } = 1000;
}
