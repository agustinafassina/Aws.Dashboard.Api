namespace Aws.Models.Dto;

public class S3PublicBucketDto
{
    public required string Name { get; init; }
    public required string Region { get; init; }
    public required IReadOnlyList<string> PublicAccessReasons { get; init; }
    public required string Recommendation { get; init; }
    public DateTime CreationDate { get; init; }
}
