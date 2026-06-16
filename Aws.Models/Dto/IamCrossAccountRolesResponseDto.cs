namespace Aws.Models.Dto;

public class IamCrossAccountRolesResponseDto
{
    public required string CurrentAccountId { get; init; }
    public int TotalRolesScanned { get; init; }
    public int CrossAccountRolesCount { get; init; }
    public required IReadOnlyList<IamCrossAccountRoleDto> Roles { get; init; }
    public DateTime ScannedAt { get; init; }
}
