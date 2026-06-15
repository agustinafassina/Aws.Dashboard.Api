using System.Net;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aws.Models.Configuration;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;

namespace Aws.Repository.Implementations;

public class S3Repository : IS3Repository
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3Repository> _logger;
    private readonly HashSet<string> _publicGranteeUris;

    public S3Repository(
        IAmazonS3 s3Client,
        IOptions<S3Options> s3Options,
        ILogger<S3Repository> logger)
    {
        _s3Client = s3Client;
        _logger = logger;

        var options = s3Options.Value;
        _publicGranteeUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            options.AllUsersUri,
            options.AuthenticatedUsersUri
        };
    }

    public async Task<S3PublicBucketsResponseDto> GetPublicBucketsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        string normalizedRegion = NormalizeRegion(region);
        ListBucketsResponse listResponse = await _s3Client.ListBucketsAsync(cancellationToken);
        List<S3Bucket> bucketsInRegion = new();

        foreach (S3Bucket bucket in listResponse.Buckets ?? [])
        {
            string bucketRegion = await GetBucketRegionAsync(bucket.BucketName, cancellationToken);
            if (string.Equals(bucketRegion, normalizedRegion, StringComparison.OrdinalIgnoreCase))
                bucketsInRegion.Add(bucket);
        }

        _logger.LogInformation(
            "Found {Count} buckets in region {Region}, analyzing public access",
            bucketsInRegion.Count,
            normalizedRegion);

        List<S3PublicBucketDto> publicBuckets = new();

        foreach (var bucket in bucketsInRegion)
        {
            (bool IsPublic, List<string> Reasons) analysis = await AnalyzePublicAccessAsync(bucket.BucketName, normalizedRegion, cancellationToken);
            if (analysis.IsPublic)
            {
                publicBuckets.Add(new S3PublicBucketDto
                {
                    Name = bucket.BucketName,
                    Region = normalizedRegion,
                    PublicAccessReasons = analysis.Reasons,
                    Recommendation = BuildRecommendation(analysis.Reasons),
                    CreationDate = bucket.CreationDate.GetValueOrDefault()
                });
            }
        }

        return new S3PublicBucketsResponseDto
        {
            Region = normalizedRegion,
            TotalBucketsInRegion = bucketsInRegion.Count,
            PublicBucketsCount = publicBuckets.Count,
            PublicBuckets = publicBuckets,
            ScannedAt = DateTime.UtcNow
        };
    }

    public async Task<S3EncryptionStatusResponseDto> GetEncryptionStatusByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        string normalizedRegion = NormalizeRegion(region);
        ListBucketsResponse listResponse = await _s3Client.ListBucketsAsync(cancellationToken);
        List<S3Bucket> bucketsInRegion = new();

        foreach (S3Bucket bucket in listResponse.Buckets ?? [])
        {
            string bucketRegion = await GetBucketRegionAsync(bucket.BucketName, cancellationToken);
            if (string.Equals(bucketRegion, normalizedRegion, StringComparison.OrdinalIgnoreCase))
                bucketsInRegion.Add(bucket);
        }

        _logger.LogInformation(
            "Found {Count} buckets in region {Region}, analyzing default encryption",
            bucketsInRegion.Count,
            normalizedRegion);

        List<S3BucketEncryptionDto> unencryptedBuckets = new();

        foreach (S3Bucket bucket in bucketsInRegion)
        {
            bool hasDefaultEncryption = await HasDefaultEncryptionAsync(
                bucket.BucketName,
                normalizedRegion,
                cancellationToken);

            if (hasDefaultEncryption)
                continue;

            unencryptedBuckets.Add(new S3BucketEncryptionDto
            {
                Name = bucket.BucketName,
                Region = normalizedRegion,
                CreationDate = bucket.CreationDate.GetValueOrDefault(),
                Recommendation = "Enable default bucket encryption with SSE-S3 or SSE-KMS for all new objects."
            });
        }

        return new S3EncryptionStatusResponseDto
        {
            Region = normalizedRegion,
            TotalBucketsInRegion = bucketsInRegion.Count,
            UnencryptedBucketsCount = unencryptedBuckets.Count,
            UnencryptedBuckets = unencryptedBuckets
                .OrderBy(bucket => bucket.Name, StringComparer.Ordinal)
                .ToList(),
            ScannedAt = DateTime.UtcNow
        };
    }

    private async Task<bool> HasDefaultEncryptionAsync(
        string bucketName,
        string bucketRegion,
        CancellationToken cancellationToken)
    {
        IAmazonS3 regionalClient = GetRegionalClient(bucketRegion);

        try
        {
            GetBucketEncryptionResponse response = await regionalClient.GetBucketEncryptionAsync(
                new GetBucketEncryptionRequest { BucketName = bucketName },
                cancellationToken);

            return response.ServerSideEncryptionConfiguration?.ServerSideEncryptionRules?
                .Any(rule =>
                    !string.IsNullOrWhiteSpace(rule.ServerSideEncryptionByDefault?.ServerSideEncryptionAlgorithm?.Value)) == true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private async Task<string> GetBucketRegionAsync(string bucketName, CancellationToken cancellationToken)
    {
        GetBucketLocationResponse response = await _s3Client.GetBucketLocationAsync(
            new GetBucketLocationRequest { BucketName = bucketName },
            cancellationToken);

        return NormalizeRegion(response.Location?.Value);
    }

    private async Task<(bool IsPublic, List<string> Reasons)> AnalyzePublicAccessAsync(
        string bucketName,
        string bucketRegion,
        CancellationToken cancellationToken)
    {
        List<string> reasons = new();
        IAmazonS3 regionalClient = GetRegionalClient(bucketRegion);

        if (await IsBlockPublicAccessFullyEnabledAsync(regionalClient, bucketName, cancellationToken))
            return (false, reasons);

        if (await IsPolicyPublicAsync(regionalClient, bucketName, cancellationToken))
            reasons.Add("BucketPolicy");

        if (await HasPublicAclGrantAsync(regionalClient, bucketName, cancellationToken))
            reasons.Add("BucketAcl");

        return (reasons.Count > 0, reasons);
    }

    private async Task<bool> IsBlockPublicAccessFullyEnabledAsync(
        IAmazonS3 client,
        string bucketName,
        CancellationToken cancellationToken)
    {
        try
        {
            GetPublicAccessBlockResponse response = await client.GetPublicAccessBlockAsync(
                new GetPublicAccessBlockRequest { BucketName = bucketName },
                cancellationToken);

            PublicAccessBlockConfiguration config = response.PublicAccessBlockConfiguration;
            return config.BlockPublicAcls == true
                && config.IgnorePublicAcls == true
                && config.BlockPublicPolicy == true
                && config.RestrictPublicBuckets == true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private async Task<bool> IsPolicyPublicAsync(
        IAmazonS3 client,
        string bucketName,
        CancellationToken cancellationToken)
    {
        try
        {
            GetBucketPolicyStatusResponse response = await client.GetBucketPolicyStatusAsync(
                new GetBucketPolicyStatusRequest { BucketName = bucketName },
                cancellationToken);

            return response.PolicyStatus?.IsPublic == true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private async Task<bool> HasPublicAclGrantAsync(
        IAmazonS3 client,
        string bucketName,
        CancellationToken cancellationToken)
    {
        try
        {
            GetBucketAclResponse response = await client.GetBucketAclAsync(
                new GetBucketAclRequest { BucketName = bucketName },
                cancellationToken);

            return response.Grants?.Any(IsPublicGrant) == true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private static readonly HashSet<string> PublicPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        S3Permission.READ,
        S3Permission.READ_ACP,
        S3Permission.FULL_CONTROL,
        S3Permission.WRITE,
        S3Permission.WRITE_ACP
    };

    private bool IsPublicGrant(S3Grant grant)
    {
        string? uri = grant.Grantee?.URI;
        if (uri == null || !_publicGranteeUris.Contains(uri))
            return false;

        S3Permission? permission = grant.Permission;
        return permission != null && PublicPermissions.Contains(permission);
    }

    private IAmazonS3 GetRegionalClient(string region)
    {
        if (string.Equals(_s3Client.Config.RegionEndpoint?.SystemName, region, StringComparison.OrdinalIgnoreCase))
            return _s3Client;

        return new AmazonS3Client(RegionEndpoint.GetBySystemName(region));
    }

    private static string NormalizeRegion(string? region)
    {
        if (string.IsNullOrWhiteSpace(region) || region.Equals("US", StringComparison.OrdinalIgnoreCase))
            return "us-east-1";

        return region;
    }

    private static string BuildRecommendation(IReadOnlyList<string> reasons)
    {
        if (reasons.Contains("BucketPolicy") && reasons.Contains("BucketAcl"))
            return "Enable S3 Block Public Access on this bucket, remove public statements from the bucket policy, and revoke public ACL grants.";

        if (reasons.Contains("BucketPolicy"))
            return "Enable S3 Block Public Access and review or remove public statements in the bucket policy.";

        return "Enable S3 Block Public Access and revoke public ACL grants (AllUsers or AuthenticatedUsers).";
    }
}
