namespace Aws.Models.Dto;

public class RdsInstancePortsDto
{
    public required string DbInstanceIdentifier { get; init; }
    public required string Engine { get; init; }
    public string? EngineVersion { get; init; }
    public required string Status { get; init; }
    public string? Endpoint { get; init; }
    public int? Port { get; init; }
    public bool PubliclyAccessible { get; init; }
    public required IReadOnlyList<SecurityGroupDto> SecurityGroups { get; init; }
    public bool HasPubliclyExposedPorts { get; init; }
    public required IReadOnlyList<InboundRuleDto> PubliclyExposedInboundRules { get; init; }
    public required string Recommendation { get; init; }
}
