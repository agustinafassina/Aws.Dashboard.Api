using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;
using Aws.Services.Interfaces;

namespace Aws.Services.Implementations;

public class S3SecurityService : IS3SecurityService
{
    private readonly IS3Repository _s3Repository;
    private readonly ILogger<S3SecurityService> _logger;

    public S3SecurityService(IS3Repository s3Repository, ILogger<S3SecurityService> logger)
    {
        _s3Repository = s3Repository;
        _logger = logger;
    }

    public async Task<S3PublicBucketsResponseDto> GetPublicBucketsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required.", nameof(region));

        _logger.LogInformation("Scanning public S3 buckets in region {Region}", region);
        S3PublicBucketsResponseDto result = await _s3Repository.GetPublicBucketsByRegionAsync(region, cancellationToken);

        _logger.LogInformation(
            "Scan complete for region {Region}: {PublicCount}/{TotalCount} buckets are publicly accessible",
            result.Region,
            result.PublicBucketsCount,
            result.TotalBucketsInRegion);

        return result;
    }

    public async Task<S3EncryptionStatusResponseDto> GetEncryptionStatusByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required.", nameof(region));

        _logger.LogInformation("Scanning S3 bucket encryption in region {Region}", region);
        S3EncryptionStatusResponseDto result =
            await _s3Repository.GetEncryptionStatusByRegionAsync(region, cancellationToken);

        _logger.LogInformation(
            "Encryption scan complete for region {Region}: {UnencryptedCount}/{TotalCount} buckets without default encryption",
            result.Region,
            result.UnencryptedBucketsCount,
            result.TotalBucketsInRegion);

        return result;
    }
}
