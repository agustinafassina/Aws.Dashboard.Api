namespace Aws.Models.Dto;

public class EcrRepositoryRiskDto
{
    public required string RepositoryName { get; init; }
    public required string RepositoryArn { get; init; }
    public bool IsPublic { get; init; }
    public bool ScanOnPushEnabled { get; init; }
    public required IReadOnlyList<string> RiskReasons { get; init; }
    public required string Recommendation { get; init; }
}
