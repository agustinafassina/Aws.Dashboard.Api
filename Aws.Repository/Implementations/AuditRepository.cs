using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.RDS;
using Amazon.RDS.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
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
    private readonly IAmazonLambda _lambdaClient;
    private readonly IAmazonSQS _sqsClient;
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly ILogger<AuditRepository> _logger;
    private readonly string _requiredTagKey;

    public AuditRepository(
        IAmazonEC2 ec2Client,
        IAmazonRDS rdsClient,
        IAmazonS3 s3Client,
        IAmazonLambda lambdaClient,
        IAmazonSQS sqsClient,
        IAmazonSimpleNotificationService snsClient,
        IOptions<CostOptions> costOptions,
        ILogger<AuditRepository> logger)
    {
        _ec2Client = ec2Client;
        _rdsClient = rdsClient;
        _s3Client = s3Client;
        _lambdaClient = lambdaClient;
        _sqsClient = sqsClient;
        _snsClient = snsClient;
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
        totalScanned += await ScanLambdaByProjectTagAsync(
            normalizedRegion,
            projectTagValue,
            matching,
            cancellationToken);
        totalScanned += await ScanSqsByProjectTagAsync(
            normalizedRegion,
            projectTagValue,
            matching,
            cancellationToken);
        totalScanned += await ScanSnsByProjectTagAsync(
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
            Amazon.RDS.Model.ListTagsForResourceResponse tagsResponse = await client.ListTagsForResourceAsync(
                new Amazon.RDS.Model.ListTagsForResourceRequest { ResourceName = instance.DBInstanceArn },
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
            Amazon.RDS.Model.ListTagsForResourceResponse tagsResponse = await client.ListTagsForResourceAsync(
                new Amazon.RDS.Model.ListTagsForResourceRequest { ResourceName = instance.DBInstanceArn },
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

    private async Task<int> ScanLambdaByProjectTagAsync(
        string region,
        string projectTagValue,
        List<TaggedResourceDto> matching,
        CancellationToken cancellationToken)
    {
        IAmazonLambda client = GetRegionalLambdaClient(region);
        List<FunctionConfiguration> functions = new();
        string? marker = null;

        do
        {
            ListFunctionsResponse response = await client.ListFunctionsAsync(
                new ListFunctionsRequest { Marker = marker },
                cancellationToken);

            if (response.Functions is not null)
                functions.AddRange(response.Functions);

            marker = response.NextMarker;
        }
        while (!string.IsNullOrEmpty(marker));

        foreach (FunctionConfiguration function in functions)
        {
            if (string.IsNullOrWhiteSpace(function.FunctionArn))
                continue;

            ListTagsResponse tagsResponse = await client.ListTagsAsync(
                new ListTagsRequest { Resource = function.FunctionArn },
                cancellationToken);

            string? tagValue = ResourceTagHelper.GetTagValue(tagsResponse.Tags, _requiredTagKey);
            if (!ResourceTagHelper.MatchesTagValue(tagValue, projectTagValue))
                continue;

            matching.Add(new TaggedResourceDto
            {
                ResourceType = "Lambda",
                ResourceId = function.FunctionName ?? function.FunctionArn,
                Name = function.FunctionName,
                Region = region,
                State = function.State?.Value,
                ProjectTagValue = tagValue!.Trim()
            });
        }

        return functions.Count;
    }

    private async Task<int> ScanSqsByProjectTagAsync(
        string region,
        string projectTagValue,
        List<TaggedResourceDto> matching,
        CancellationToken cancellationToken)
    {
        IAmazonSQS client = GetRegionalSqsClient(region);
        List<string> queueUrls = new();
        string? nextToken = null;

        do
        {
            ListQueuesResponse response = await client.ListQueuesAsync(
                new ListQueuesRequest { NextToken = nextToken },
                cancellationToken);

            if (response.QueueUrls is not null)
                queueUrls.AddRange(response.QueueUrls);

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        foreach (string queueUrl in queueUrls)
        {
            ListQueueTagsResponse tagsResponse = await client.ListQueueTagsAsync(
                new ListQueueTagsRequest { QueueUrl = queueUrl },
                cancellationToken);

            string? tagValue = ResourceTagHelper.GetTagValue(tagsResponse.Tags, _requiredTagKey);
            if (!ResourceTagHelper.MatchesTagValue(tagValue, projectTagValue))
                continue;

            string queueName = GetQueueNameFromUrl(queueUrl);

            matching.Add(new TaggedResourceDto
            {
                ResourceType = "SQS",
                ResourceId = queueName,
                Name = queueName,
                Region = region,
                State = null,
                ProjectTagValue = tagValue!.Trim()
            });
        }

        return queueUrls.Count;
    }

    private async Task<int> ScanSnsByProjectTagAsync(
        string region,
        string projectTagValue,
        List<TaggedResourceDto> matching,
        CancellationToken cancellationToken)
    {
        IAmazonSimpleNotificationService client = GetRegionalSnsClient(region);
        List<Topic> topics = new();
        string? nextToken = null;

        do
        {
            ListTopicsResponse response = await client.ListTopicsAsync(
                new ListTopicsRequest { NextToken = nextToken },
                cancellationToken);

            if (response.Topics is not null)
                topics.AddRange(response.Topics);

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        foreach (Topic topic in topics)
        {
            if (string.IsNullOrWhiteSpace(topic.TopicArn))
                continue;

            Amazon.SimpleNotificationService.Model.ListTagsForResourceResponse tagsResponse =
                await client.ListTagsForResourceAsync(
                    new Amazon.SimpleNotificationService.Model.ListTagsForResourceRequest
                    {
                        ResourceArn = topic.TopicArn
                    },
                    cancellationToken);

            string? tagValue = ResourceTagHelper.GetTagValue(
                ToTagDictionary(tagsResponse.Tags),
                _requiredTagKey);
            if (!ResourceTagHelper.MatchesTagValue(tagValue, projectTagValue))
                continue;

            string topicName = GetSnsTopicNameFromArn(topic.TopicArn);

            matching.Add(new TaggedResourceDto
            {
                ResourceType = "SNS",
                ResourceId = topic.TopicArn,
                Name = topicName,
                Region = region,
                State = null,
                ProjectTagValue = tagValue!.Trim()
            });
        }

        return topics.Count;
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

    private IAmazonLambda GetRegionalLambdaClient(string region)
    {
        if (string.Equals(_lambdaClient.Config.RegionEndpoint?.SystemName, region, StringComparison.OrdinalIgnoreCase))
            return _lambdaClient;

        return new AmazonLambdaClient(RegionEndpoint.GetBySystemName(region));
    }

    private IAmazonSQS GetRegionalSqsClient(string region)
    {
        if (string.Equals(_sqsClient.Config.RegionEndpoint?.SystemName, region, StringComparison.OrdinalIgnoreCase))
            return _sqsClient;

        return new AmazonSQSClient(RegionEndpoint.GetBySystemName(region));
    }

    private IAmazonSimpleNotificationService GetRegionalSnsClient(string region)
    {
        if (string.Equals(_snsClient.Config.RegionEndpoint?.SystemName, region, StringComparison.OrdinalIgnoreCase))
            return _snsClient;

        return new AmazonSimpleNotificationServiceClient(RegionEndpoint.GetBySystemName(region));
    }

    private static Dictionary<string, string>? ToTagDictionary(
        IEnumerable<Amazon.SimpleNotificationService.Model.Tag>? tags)
    {
        if (tags is null)
            return null;

        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (Amazon.SimpleNotificationService.Model.Tag tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag.Key))
                result[tag.Key] = tag.Value ?? string.Empty;
        }

        return result.Count == 0 ? null : result;
    }

    private static string GetQueueNameFromUrl(string queueUrl)
    {
        int lastSlash = queueUrl.LastIndexOf('/');
        return lastSlash >= 0 ? queueUrl[(lastSlash + 1)..] : queueUrl;
    }

    private static string GetSnsTopicNameFromArn(string topicArn)
    {
        int lastColon = topicArn.LastIndexOf(':');
        return lastColon >= 0 ? topicArn[(lastColon + 1)..] : topicArn;
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
