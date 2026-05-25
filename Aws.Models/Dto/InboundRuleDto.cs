namespace Aws.Models.Dto;

public class InboundRuleDto
{
    public required string Protocol { get; init; }
    public int? FromPort { get; init; }
    public int? ToPort { get; init; }
    public required string PortRange { get; init; }
    public required string Source { get; init; }
    public required string SourceType { get; init; }
    public bool IsPubliclyExposed { get; init; }
    public string? Description { get; init; }
}
