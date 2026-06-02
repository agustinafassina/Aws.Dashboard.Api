namespace Aws.Models.Dto;

public class IamRiskyPoliciesResponseDto
{
    public int TotalCustomerPoliciesScanned { get; init; }
    public int RiskyPolicyCount { get; init; }
    public required IReadOnlyList<IamRiskyPolicyDto> Policies { get; init; }
    public DateTime ScannedAt { get; init; }
}
