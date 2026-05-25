namespace Aws.Models.Dto;

public class SecurityGroupDto
{
    public required string GroupId { get; init; }
    public required string GroupName { get; init; }
    public string? Description { get; init; }
    public required IReadOnlyList<InboundRuleDto> InboundRules { get; init; }
}
