namespace Aws.Models.Dto;

public class ElbPublicListenerDto
{
    public required string LoadBalancerName { get; init; }
    public required string LoadBalancerArn { get; init; }
    public required string LoadBalancerType { get; init; }
    public required string Scheme { get; init; }
    public required IReadOnlyList<ElbListenerDetailDto> Listeners { get; init; }
    public required IReadOnlyList<string> RiskReasons { get; init; }
    public required string Recommendation { get; init; }
}
