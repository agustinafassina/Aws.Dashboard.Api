namespace Aws.Models.Dto;

public class IamOverprivilegedPoliciesResponseDto
{
    public int TotalCustomerPoliciesScanned { get; init; }
    public int OverprivilegedPolicyCount { get; init; }
    public int CriticalPolicyCount { get; init; }
    public int HighPolicyCount { get; init; }
    public int MediumPolicyCount { get; init; }
    public required IReadOnlyList<IamOverprivilegedPolicyDto> Policies { get; init; }
    public DateTime ScannedAt { get; init; }
}
