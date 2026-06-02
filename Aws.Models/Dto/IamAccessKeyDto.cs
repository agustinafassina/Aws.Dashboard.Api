namespace Aws.Models.Dto;

public class IamAccessKeyDto
{
    public required string UserName { get; init; }
    public required string AccessKeyId { get; init; }
    public required string Status { get; init; }
    public DateTime CreateDate { get; init; }
    public int AgeInDays { get; init; }
    public DateTime? LastUsedDate { get; init; }
    public string? LastUsedService { get; init; }
    public string? LastUsedRegion { get; init; }
    public bool NeedsRotation { get; init; }
    public bool IsNeverUsed { get; init; }
    public required string Recommendation { get; init; }
}
