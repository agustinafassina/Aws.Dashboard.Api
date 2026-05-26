namespace Aws.Models.Dto;

public class CostByProjectResponseDto
{
    public required string StartDate { get; init; }
    public required string EndDate { get; init; }
    public decimal TotalAmount { get; init; }
    public required string Currency { get; init; }
    public required string ProjectTagKey { get; init; }
    public required IReadOnlyList<ProjectCostDto> Projects { get; init; }
    public DateTime ScannedAt { get; init; }
}