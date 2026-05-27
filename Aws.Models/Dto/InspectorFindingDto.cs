namespace Aws.Models.Dto;

public class InspectorFindingDto
{
    public required string FindingArn { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string Severity { get; init; }
    public required string Status { get; init; }
    public required string Type { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public string? VulnerabilityId { get; init; }
    public string? FixAvailable { get; init; }
    public string? ExploitAvailable { get; init; }
    public DateTime? FirstObservedAt { get; init; }
    public DateTime? LastObservedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? Recommendation { get; init; }
    public InspectorFindingResourceDto? Resource { get; init; }
}
