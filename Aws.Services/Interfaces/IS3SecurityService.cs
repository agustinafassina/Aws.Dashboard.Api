using Aws.Models.Dto;

namespace Aws.Services.Interfaces;

public interface IS3SecurityService
{
    Task<S3PublicBucketsResponseDto> GetPublicBucketsByRegionAsync(string region, CancellationToken cancellationToken = default);
}
