namespace Aws.Models.Dto;

public class TaggedResourceDto
{
    public required string ResourceType { get; init; }
    public required string ResourceId { get; init; }
    public string? Name { get; init; }
    public required string Region { get; init; }
    public string? State { get; init; }
    public required string ProjectTagValue { get; init; }
}
