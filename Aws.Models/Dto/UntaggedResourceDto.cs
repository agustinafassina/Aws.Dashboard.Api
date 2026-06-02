namespace Aws.Models.Dto;

public class UntaggedResourceDto
{
    public required string ResourceType { get; init; }
    public required string ResourceId { get; init; }
    public string? Name { get; init; }
    public required string Region { get; init; }
    public string? State { get; init; }
    public required string Recommendation { get; init; }
}
