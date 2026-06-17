namespace Aws.Models.Dto;

public class Ec2Imdsv1InstancesResponseDto
{
    public required string Region { get; init; }
    public int TotalInstances { get; init; }
    public int Imdsv1InstancesCount { get; init; }
    public required IReadOnlyList<Ec2Imdsv1InstanceDto> Instances { get; init; }
    public DateTime ScannedAt { get; init; }
}
