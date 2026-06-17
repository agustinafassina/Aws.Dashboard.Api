namespace Aws.Models.Dto;

public class RdsUnencryptedInstancesResponseDto
{
    public required string Region { get; init; }
    public int TotalInstances { get; init; }
    public int UnencryptedInstancesCount { get; init; }
    public required IReadOnlyList<RdsUnencryptedInstanceDto> Instances { get; init; }
    public DateTime ScannedAt { get; init; }
}
