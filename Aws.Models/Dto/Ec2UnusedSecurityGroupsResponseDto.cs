namespace Aws.Models.Dto;

public class Ec2UnusedSecurityGroupsResponseDto
{
    public required string Region { get; init; }
    public int TotalSecurityGroups { get; init; }
    public int UnusedSecurityGroupsCount { get; init; }
    public required IReadOnlyList<Ec2UnusedSecurityGroupDto> SecurityGroups { get; init; }
    public DateTime ScannedAt { get; init; }
}
