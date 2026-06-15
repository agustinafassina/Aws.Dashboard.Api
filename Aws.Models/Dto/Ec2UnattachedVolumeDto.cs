namespace Aws.Models.Dto;

public class Ec2UnattachedVolumeDto
{
    public required string VolumeId { get; init; }
    public string? Name { get; init; }
    public int SizeGiB { get; init; }
    public string? VolumeType { get; init; }
    public string? State { get; init; }
    public bool Encrypted { get; init; }
    public string? SnapshotId { get; init; }
    public string? AvailabilityZone { get; init; }
    public DateTime? CreatedAt { get; init; }
    public required string Recommendation { get; init; }
}
