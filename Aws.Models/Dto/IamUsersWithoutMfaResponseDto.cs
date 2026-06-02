namespace Aws.Models.Dto;

public class IamUsersWithoutMfaResponseDto
{
    public int TotalUsers { get; init; }
    public int UsersWithConsoleAccess { get; init; }
    public int UsersWithoutMfa { get; init; }
    public required IReadOnlyList<IamUserWithoutMfaDto> Users { get; init; }
    public DateTime ScannedAt { get; init; }
}
