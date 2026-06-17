namespace Aws.Models.Dto;

public class IamOverprivilegedPolicyDto
{
    public required string PolicyArn { get; init; }
    public required string PolicyName { get; init; }
    public required string PrivilegeLevel { get; init; }
    public required string RiskReason { get; init; }
    public required IReadOnlyList<string> RiskyActions { get; init; }
    public required string Recommendation { get; init; }
}
