namespace Aws.Models.Dto;

public class Ec2UnattachedVolumesResponseDto
{
    public required string Region { get; init; }
    public int TotalVolumes { get; init; }
    public int UnattachedVolumesCount { get; init; }
    public int TotalUnattachedSizeGiB { get; init; }
    public required IReadOnlyList<Ec2UnattachedVolumeDto> Volumes { get; init; }
    public DateTime ScannedAt { get; init; }
}
