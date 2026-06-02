namespace Aws.Models.Dto;

public class UntaggedResourcesResponseDto
{
    public required string Region { get; init; }
    public required string RequiredTagKey { get; init; }
    public int TotalResourcesScanned { get; init; }
    public int UntaggedResourcesCount { get; init; }
    public required IReadOnlyList<UntaggedResourceDto> Resources { get; init; }
    public DateTime ScannedAt { get; init; }
}
