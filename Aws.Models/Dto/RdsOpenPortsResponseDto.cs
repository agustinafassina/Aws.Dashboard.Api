namespace Aws.Models.Dto;

public class RdsOpenPortsResponseDto
{
    public required string Region { get; init; }
    public int TotalInstances { get; init; }
    public int InstancesWithPublicPorts { get; init; }
    public required IReadOnlyList<RdsInstancePortsDto> Instances { get; init; }
    public DateTime ScannedAt { get; init; }
}
