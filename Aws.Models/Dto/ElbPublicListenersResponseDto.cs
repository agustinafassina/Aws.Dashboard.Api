namespace Aws.Models.Dto;

public class ElbPublicListenersResponseDto
{
    public required string Region { get; init; }
    public int TotalLoadBalancersScanned { get; init; }
    public int PublicLoadBalancersCount { get; init; }
    public required IReadOnlyList<ElbPublicListenerDto> LoadBalancers { get; init; }
    public DateTime ScannedAt { get; init; }
}
