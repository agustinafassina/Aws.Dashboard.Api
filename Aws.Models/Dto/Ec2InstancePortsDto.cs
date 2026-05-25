namespace Aws.Models.Dto;

public class Ec2InstancePortsDto
{
    public required string InstanceId { get; init; }
    public string? Name { get; init; }
    public required string State { get; init; }
    public string? InstanceType { get; init; }
    public string? PrivateIpAddress { get; init; }
    public string? PublicIpAddress { get; init; }
    public required IReadOnlyList<SecurityGroupDto> SecurityGroups { get; init; }
    public bool HasPubliclyExposedPorts { get; init; }
    public required IReadOnlyList<InboundRuleDto> PubliclyExposedInboundRules { get; init; }
    public required string Recommendation { get; init; }
}
