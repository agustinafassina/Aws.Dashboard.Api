namespace Aws.Models.Dto;

public class S3EncryptionStatusResponseDto
{
    public required string Region { get; init; }
    public int TotalBucketsInRegion { get; init; }
    public int UnencryptedBucketsCount { get; init; }
    public required IReadOnlyList<S3BucketEncryptionDto> UnencryptedBuckets { get; init; }
    public DateTime ScannedAt { get; init; }
}
