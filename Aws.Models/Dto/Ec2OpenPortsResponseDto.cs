namespace Aws.Models.Dto;

public class Ec2OpenPortsResponseDto
{
    public required string Region { get; init; }
    public int TotalInstances { get; init; }
    public int InstancesWithPublicPorts { get; init; }
    public required IReadOnlyList<Ec2InstancePortsDto> Instances { get; init; }
    public DateTime ScannedAt { get; init; }
}
