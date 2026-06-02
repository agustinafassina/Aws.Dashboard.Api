using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.RDS;
using Amazon.RDS.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aws.Models.Configuration;
using Aws.Models.Dto;
using Aws.Repository.Helpers;
using Aws.Repository.Interfaces;
using Ec2Tag = Amazon.EC2.Model.Tag;

namespace Aws.Repository.Implementations;

public class AuditRepository : IAuditRepository
{
    private readonly IAmazonEC2 _ec2Client;
    private readonly IAmazonRDS _rdsClient;
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<AuditRepository> _logger;
    private readonly string _requiredTagKey;

    public AuditRepository(
        IAmazonEC2 ec2Client,
        IAmazonRDS rdsClient,
        IAmazonS3 s3Client,
        IOptions<CostOptions> costOptions,
        ILogger<AuditRepository> logger)
    {
        _ec2Client = ec2Client;
        _rdsClient = rdsClient;
        _s3Client = s3Client;
        _logger = logger;
        _requiredTagKey = string.IsNullOrWhiteSpace(costOptions.Value.ProjectTagKey)
            ? "Project"
            : costOptions.Value.ProjectTagKey;
    }

    public async Task<UntaggedResourcesResponseDto> GetUntaggedResourcesByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        string normalizedRegion = NormalizeRegion(region);
        List<UntaggedResourceDto> untagged = new();
        int totalScanned = 0;

        totalScanned += await ScanEc2Async(normalizedRegion, untagged, cancellationToken);
        totalScanned += await ScanRdsAsync(normalizedRegion, untagged, cancellationToken);
        totalScanned += await ScanS3Async(normalizedRegion, untagged, cancellationToken);

        _logger.LogInformation(
            "Untagged resource audit in {Region}: {UntaggedCount}/{TotalScanned} missing tag {TagKey}",
            normalizedRegion,
            untagged.Count,
            totalScanned,
            _requiredTagKey);

        return new UntaggedResourcesResponseDto
        {
            Region = normalizedRegion,
            RequiredTagKey = _requiredTagKey,
            TotalResourcesScanned = totalScanned,
            UntaggedResourcesCount = untagged.Count,
            Resources = untagged
                .OrderBy(r => r.ResourceType, StringComparer.Ordinal)
                .ThenBy(r => r.ResourceId, StringComparer.Ordinal)
                .ToList(),
            ScannedAt = DateTime.UtcNow
        };
    }

    public async Task<ResourcesByProjectTagResponseDto> GetResourcesByProjectTagAsync(
        string region,
        string projectTagValue,
        CancellationToken cancellationToken = default)
    {
        string normalizedRegion = NormalizeRegion(region);
        List<TaggedResourceDto> matching = new();
        int totalScanned = 0;

        totalScanned += await ScanEc2ByProjectTagAsync(
            normalizedRegion,
            projectTagValue,
            matching,
            cancellationToken);
        totalScanned += await ScanRdsByProjectTagAsync(
            normalizedRegion,
            projectTagValue,
            matching,
            cancellationToken);
        totalScanned += await ScanS3ByProjectTagAsync(
            normalizedRegion,
            projectTagValue,
            matching,
            cancellationToken);

        _logger.LogInformation(
            "Project tag search in {Region}: {MatchCount}/{TotalScanned} with {TagKey}={TagValue}",
            normalizedRegion,
            matching.Count,
            totalScanned,
            _requiredTagKey,
            projectTagValue);

        return new ResourcesByProjectTagResponseDto
        {
            Region = normalizedRegion,
            ProjectTagKey = _requiredTagKey,
            ProjectTagValue = projectTagValue,
            TotalResourcesScanned = totalScanned,
            MatchingResourcesCount = matching.Count,
            Resources = matching
                .OrderBy(r => r.ResourceType, StringComparer.Ordinal)
                .ThenBy(r => r.ResourceId, StringComparer.Ordinal)
                .ToList(),
            ScannedAt = DateTime.UtcNow
        };
    }

    private async Task<int> ScanEc2Async(
        string region,
        List<UntaggedResourceDto> untagged,
        CancellationToken cancellationToken)
    {
        IAmazonEC2 client = GetRegionalEc2Client(region);
        List<Instance> instances = new();
        string? nextToken = null;

        do
        {
            DescribeInstancesResponse response = await client.DescribeInstancesAsync(
                new DescribeInstancesRequest { NextToken = nextToken },
                cancellationToken);

            foreach (Reservation reservation in response.Reservations ?? [])
            {
                if (reservation.Instances is not null)
                    instances.AddRange(reservation.Instances);
            }

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        foreach (Instance instance in instances)
        {
            if (ResourceTagHelper.HasRequiredTag(instance.Tags, _requiredTagKey))
                continue;

            string? name = instance.Tags?
                .FirstOrDefault(t => string.Equals(t.Key, "Name", StringComparison.OrdinalIgnoreCase))
                ?.Value;

            untagged.Add(new UntaggedResourceDto
            {
                ResourceType = "EC2",
                ResourceId = instance.InstanceId,
                Name = name,
                Region = region,
                State = instance.State?.Name?.Value,
                Recommendation = ResourceTagHelper.BuildUntaggedRecommendation(_requiredTagKey)
            });
        }

        return instances.Count;
    }

    private async Task<int> ScanRdsAsync(
        string region,
        List<UntaggedResourceDto> untagged,
        CancellationToken cancellationToken)
    {
        IAmazonRDS client = GetRegionalRdsClient(region);
        List<DBInstance> instances = new();
        string? marker = null;

        do
        {
            DescribeDBInstancesResponse response = await client.DescribeDBInstancesAsync(
                new DescribeDBInstancesRequest { Marker = marker },
                cancellationToken);

            if (response.DBInstances is not null)
                instances.AddRange(response.DBInstances);

            marker = response.Marker;
        }
        while (!string.IsNullOrEmpty(marker));

        foreach (DBInstance instance in instances)
        {
            ListTagsForResourceResponse tagsResponse = await client.ListTagsForResourceAsync(
                new ListTagsForResourceRequest { ResourceName = instance.DBInstanceArn },
                cancellationToken);

            if (ResourceTagHelper.HasRequiredTag(tagsResponse.TagList, _requiredTagKey))
                continue;

            untagged.Add(new UntaggedResourceDto
            {
                ResourceType = "RDS",
                ResourceId = instance.DBInstanceIdentifier,
                Name = instance.DBInstanceIdentifier,
                Region = region,
                State = instance.DBInstanceStatus,
                Recommendation = ResourceTagHelper.BuildUntaggedRecommendation(_requiredTagKey)
            });
        }

        return instances.Count;
    }

    private async Task<int> ScanS3Async(
        string region,
        List<UntaggedResourceDto> untagged,
        CancellationToken cancellationToken)
    {
        ListBucketsResponse listResponse = await _s3Client.ListBucketsAsync(cancellationToken);
        int countInRegion = 0;

        foreach (S3Bucket bucket in listResponse.Buckets ?? [])
        {
            string bucketRegion = await GetBucketRegionAsync(bucket.BucketName, cancellationToken);
            if (!string.Equals(bucketRegion, region, StringComparison.OrdinalIgnoreCase))
                continue;

            countInRegion++;

            try
            {
                IAmazonS3 regionalClient = GetRegionalS3Client(bucketRegion);
                GetBucketTaggingResponse tagging = await regionalClient.GetBucketTaggingAsync(
                    new GetBucketTaggingRequest { BucketName = bucket.BucketName },
                    cancellationToken);

                if (ResourceTagHelper.HasRequiredTag(tagging.TagSet, _requiredTagKey))
                    continue;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // No tags on bucket — missing required tag
            }

            untagged.Add(new UntaggedResourceDto
            {
                ResourceType = "S3",
                ResourceId = bucket.BucketName,
                Name = bucket.BucketName,
                Region = region,
                State = null,
                Recommendation = ResourceTagHelper.BuildUntaggedRecommendation(_requiredTagKey)
            });
        }

        return countInRegion;
    }

    private async Task<int> ScanEc2ByProjectTagAsync(
        string region,
        string projectTagValue,
        List<TaggedResourceDto> matching,
        CancellationToken cancellationToken)
    {
        IAmazonEC2 client = GetRegionalEc2Client(region);
        List<Instance> instances = new();
        string? nextToken = null;

        do
        {
            DescribeInstancesResponse response = await client.DescribeInstancesAsync(
                new DescribeInstancesRequest { NextToken = nextToken },
                cancellationToken);

            foreach (Reservation reservation in response.Reservations ?? [])
            {
                if (reservation.Instances is not null)
                    instances.AddRange(reservation.Instances);
            }

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        foreach (Instance instance in instances)
        {
            string? tagValue = ResourceTagHelper.GetTagValue(instance.Tags, _requiredTagKey);
            if (!ResourceTagHelper.MatchesTagValue(tagValue, projectTagValue))
                continue;

            string? name = instance.Tags?
                .FirstOrDefault(t => string.Equals(t.Key, "Name", StringComparison.OrdinalIgnoreCase))
                ?.Value;

            matching.Add(new TaggedResourceDto
            {
                ResourceType = "EC2",
                ResourceId = instance.InstanceId,
                Name = name,
                Region = region,
                State = instance.State?.Name?.Value,
                ProjectTagValue = tagValue!.Trim()
            });
        }

        return instances.Count;
    }

    private async Task<int> ScanRdsByProjectTagAsync(
        string region,
        string projectTagValue,
        List<TaggedResourceDto> matching,
        CancellationToken cancellationToken)
    {
        IAmazonRDS client = GetRegionalRdsClient(region);
        List<DBInstance> instances = new();
        string? marker = null;

        do
        {
            DescribeDBInstancesResponse response = await client.DescribeDBInstancesAsync(
                new DescribeDBInstancesRequest { Marker = marker },
                cancellationToken);

            if (response.DBInstances is not null)
                instances.AddRange(response.DBInstances);

            marker = response.Marker;
        }
        while (!string.IsNullOrEmpty(marker));

        foreach (DBInstance instance in instances)
        {
            ListTagsForResourceResponse tagsResponse = await client.ListTagsForResourceAsync(
                new ListTagsForResourceRequest { ResourceName = instance.DBInstanceArn },
                cancellationToken);

            string? tagValue = ResourceTagHelper.GetTagValue(tagsResponse.TagList, _requiredTagKey);
            if (!ResourceTagHelper.MatchesTagValue(tagValue, projectTagValue))
                continue;

            matching.Add(new TaggedResourceDto
            {
                ResourceType = "RDS",
                ResourceId = instance.DBInstanceIdentifier,
                Name = instance.DBInstanceIdentifier,
                Region = region,
                State = instance.DBInstanceStatus,
                ProjectTagValue = tagValue!.Trim()
            });
        }

        return instances.Count;
    }

    private async Task<int> ScanS3ByProjectTagAsync(
        string region,
        string projectTagValue,
        List<TaggedResourceDto> matching,
        CancellationToken cancellationToken)
    {
        ListBucketsResponse listResponse = await _s3Client.ListBucketsAsync(cancellationToken);
        int countInRegion = 0;

        foreach (S3Bucket bucket in listResponse.Buckets ?? [])
        {
            string bucketRegion = await GetBucketRegionAsync(bucket.BucketName, cancellationToken);
            if (!string.Equals(bucketRegion, region, StringComparison.OrdinalIgnoreCase))
                continue;

            countInRegion++;

            string? tagValue = null;

            try
            {
                IAmazonS3 regionalClient = GetRegionalS3Client(bucketRegion);
                GetBucketTaggingResponse tagging = await regionalClient.GetBucketTaggingAsync(
                    new GetBucketTaggingRequest { BucketName = bucket.BucketName },
                    cancellationToken);

                tagValue = ResourceTagHelper.GetTagValue(tagging.TagSet, _requiredTagKey);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Bucket has no tags
            }

            if (!ResourceTagHelper.MatchesTagValue(tagValue, projectTagValue))
                continue;

            matching.Add(new TaggedResourceDto
            {
                ResourceType = "S3",
                ResourceId = bucket.BucketName,
                Name = bucket.BucketName,
                Region = region,
                State = null,
                ProjectTagValue = tagValue!.Trim()
            });
        }

        return countInRegion;
    }

    private async Task<string> GetBucketRegionAsync(string bucketName, CancellationToken cancellationToken)
    {
        try
        {
            GetBucketLocationResponse location = await _s3Client.GetBucketLocationAsync(
                new GetBucketLocationRequest { BucketName = bucketName },
                cancellationToken);

            return NormalizeS3Region(location.Location?.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve region for bucket {BucketName}", bucketName);
            return "unknown";
        }
    }

    private IAmazonEC2 GetRegionalEc2Client(string region)
    {
        if (string.Equals(_ec2Client.Config.RegionEndpoint?.SystemName, region, StringComparison.OrdinalIgnoreCase))
            return _ec2Client;

        return new AmazonEC2Client(RegionEndpoint.GetBySystemName(region));
    }

    private IAmazonRDS GetRegionalRdsClient(string region)
    {
        if (string.Equals(_rdsClient.Config.RegionEndpoint?.SystemName, region, StringComparison.OrdinalIgnoreCase))
            return _rdsClient;

        return new AmazonRDSClient(RegionEndpoint.GetBySystemName(region));
    }

    private IAmazonS3 GetRegionalS3Client(string region)
    {
        string normalized = NormalizeS3Region(region);
        if (string.Equals(_s3Client.Config.RegionEndpoint?.SystemName, normalized, StringComparison.OrdinalIgnoreCase))
            return _s3Client;

        return new AmazonS3Client(RegionEndpoint.GetBySystemName(normalized));
    }

    private static string NormalizeS3Region(string? region)
    {
        if (string.IsNullOrWhiteSpace(region) || region.Equals("US", StringComparison.OrdinalIgnoreCase))
            return "us-east-1";

        return region;
    }

    private static string NormalizeRegion(string region) =>
        string.IsNullOrWhiteSpace(region) ? "us-east-1" : region;
}
