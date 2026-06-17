namespace Aws.Models.Dto;

public class IamInlinePolicyDto
{
    public required string PrincipalType { get; init; }
    public required string PrincipalName { get; init; }
    public string? PrincipalArn { get; init; }
    public required string PolicyName { get; init; }
    public bool HasWildcardAdminRisk { get; init; }
    public string? RiskReason { get; init; }
    public required string Recommendation { get; init; }
}
