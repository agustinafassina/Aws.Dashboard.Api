namespace Aws.Models.Dto;

public class InspectorVulnerabilitiesResponseDto
{
    public required string Region { get; init; }
    public string? ResourceTypeFilter { get; init; }
    public string? SeverityFilter { get; init; }
    public string? StatusFilter { get; init; }
    public int TotalFindings { get; init; }
    public bool HasMoreFindings { get; init; }
    public required IReadOnlyList<InspectorFindingDto> Findings { get; init; }
    public DateTime ScannedAt { get; init; }
}
