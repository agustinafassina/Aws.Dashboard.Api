namespace Aws.Models.Dto;

public class Ec2UnusedSecurityGroupDto
{
    public required string GroupId { get; init; }
    public required string GroupName { get; init; }
    public string? Description { get; init; }
    public string? VpcId { get; init; }
    public bool IsDefaultSecurityGroup { get; init; }
    public int InboundRuleCount { get; init; }
    public int OutboundRuleCount { get; init; }
    public required string Recommendation { get; init; }
}
