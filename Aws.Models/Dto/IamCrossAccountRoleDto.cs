namespace Aws.Models.Dto;

public class IamCrossAccountRoleDto
{
    public required string RoleName { get; init; }
    public required string RoleArn { get; init; }
    public required string TrustRiskReason { get; init; }
    public required IReadOnlyList<string> TrustedPrincipals { get; init; }
    public bool AllowsAnyPrincipal { get; init; }
    public required string Recommendation { get; init; }
}
