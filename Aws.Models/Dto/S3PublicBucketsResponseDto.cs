namespace Aws.Models.Dto;

public class S3PublicBucketsResponseDto
{
    public required string Region { get; init; }
    public int TotalBucketsInRegion { get; init; }
    public int PublicBucketsCount { get; init; }
    public required IReadOnlyList<S3PublicBucketDto> PublicBuckets { get; init; }
    public DateTime ScannedAt { get; init; }
}
