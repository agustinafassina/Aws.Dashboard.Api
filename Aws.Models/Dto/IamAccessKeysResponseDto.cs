namespace Aws.Models.Dto;

public class IamAccessKeysResponseDto
{
    public int TotalUsers { get; init; }
    public int TotalAccessKeys { get; init; }
    public int AccessKeysNeedingRotation { get; init; }
    public int AccessKeyRotationMaxAgeDays { get; init; }
    public required IReadOnlyList<IamAccessKeyDto> AccessKeys { get; init; }
    public DateTime ScannedAt { get; init; }
}
