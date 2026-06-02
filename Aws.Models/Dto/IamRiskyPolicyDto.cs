namespace Aws.Models.Dto;

public class IamRiskyPolicyDto
{
    public required string PolicyArn { get; init; }
    public required string PolicyName { get; init; }
    public required string RiskReason { get; init; }
    public required string Recommendation { get; init; }
}
