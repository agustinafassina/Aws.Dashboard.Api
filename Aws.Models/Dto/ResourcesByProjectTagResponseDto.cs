namespace Aws.Models.Dto;

public class ResourcesByProjectTagResponseDto
{
    public required string Region { get; init; }
    public required string ProjectTagKey { get; init; }
    public required string ProjectTagValue { get; init; }
    public int TotalResourcesScanned { get; init; }
    public int MatchingResourcesCount { get; init; }
    public required IReadOnlyList<TaggedResourceDto> Resources { get; init; }
    public DateTime ScannedAt { get; init; }
}
