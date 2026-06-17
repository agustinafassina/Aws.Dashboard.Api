namespace Aws.Models.Dto;

public class RdsNoBackupsResponseDto
{
    public required string Region { get; init; }
    public int MinBackupRetentionDays { get; init; }
    public int TotalInstances { get; init; }
    public int InstancesWithoutAdequateBackupsCount { get; init; }
    public required IReadOnlyList<RdsNoBackupInstanceDto> Instances { get; init; }
    public DateTime ScannedAt { get; init; }
}
