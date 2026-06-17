namespace Aws.Models.Dto;

public class Ec2Imdsv1InstanceDto
{
    public required string InstanceId { get; init; }
    public string? Name { get; init; }
    public string? State { get; init; }
    public string? InstanceType { get; init; }
    public string? HttpTokens { get; init; }
    public string? HttpEndpoint { get; init; }
    public required string Recommendation { get; init; }
}
