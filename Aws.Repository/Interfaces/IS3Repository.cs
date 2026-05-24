using Aws.Models.Dto;

namespace Aws.Repository.Interfaces;

public interface IS3Repository
{
    Task<S3PublicBucketsResponseDto> GetPublicBucketsByRegionAsync(string region, CancellationToken cancellationToken = default);
}
