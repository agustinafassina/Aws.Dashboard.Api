namespace Aws.Models.Dto;

public class IamUserWithoutMfaDto
{
    public required string UserName { get; init; }
    public string? Email { get; init; }
    public DateTime? UserCreated { get; init; }
    public required string Recommendation { get; init; }
}
