namespace Aws.Models.Dto;

public class ProjectCostDto
{
    public required string Project { get; init; }
    public decimal Amount { get; init; }
    public required string Currency { get; init; }
}