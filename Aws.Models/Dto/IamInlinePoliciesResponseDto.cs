namespace Aws.Models.Dto;

public class IamInlinePoliciesResponseDto
{
    public int TotalUsersScanned { get; init; }
    public int TotalGroupsScanned { get; init; }
    public int TotalRolesScanned { get; init; }
    public int InlinePolicyCount { get; init; }
    public int RiskyInlinePolicyCount { get; init; }
    public required IReadOnlyList<IamInlinePolicyDto> Policies { get; init; }
    public DateTime ScannedAt { get; init; }
}
