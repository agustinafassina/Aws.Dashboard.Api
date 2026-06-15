namespace Aws.Models.Dto;

public class S3BucketEncryptionDto
{
    public required string Name { get; init; }
    public required string Region { get; init; }
    public DateTime CreationDate { get; init; }
    public required string Recommendation { get; init; }
}
