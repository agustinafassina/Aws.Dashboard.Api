namespace Aws.Models.Dto;

public class IamAdminPrivilegeGrantDto
{
    public required string PrincipalType { get; init; }
    public required string PrincipalName { get; init; }
    public string? PrincipalArn { get; init; }
    public required string PolicyArn { get; init; }
    public required string PolicyName { get; init; }
    public required string PrivilegeLevel { get; init; }
    public required string AttachmentType { get; init; }
    public string? InheritedFromGroup { get; init; }
    public required string Recommendation { get; init; }
}
