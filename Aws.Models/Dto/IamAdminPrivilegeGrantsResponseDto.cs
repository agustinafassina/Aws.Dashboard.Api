namespace Aws.Models.Dto;

public class IamAdminPrivilegeGrantsResponseDto
{
    public int TotalUsersScanned { get; init; }
    public int TotalGroupsScanned { get; init; }
    public int TotalRolesScanned { get; init; }
    public int AdminPrivilegeGrantCount { get; init; }
    public int UsersWithAdminAccess { get; init; }
    public int GroupsWithAdminAccess { get; init; }
    public int RolesWithAdminAccess { get; init; }
    public required IReadOnlyList<IamAdminPrivilegeGrantDto> Grants { get; init; }
    public DateTime ScannedAt { get; init; }
}
