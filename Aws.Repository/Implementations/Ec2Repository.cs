using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aws.Models.Configuration;
using Aws.Models.Dto;
using Aws.Repository.Helpers;
using Aws.Repository.Interfaces;

namespace Aws.Repository.Implementations;

public class Ec2Repository : IEc2Repository
{
    private const int SecurityGroupBatchSize = 200;

    private readonly IAmazonEC2 _ec2Client;
    private readonly ILogger<Ec2Repository> _logger;
    private readonly HashSet<string> _publicInboundCidrs;

    public Ec2Repository(
        IAmazonEC2 ec2Client,
        IOptions<Ec2Options> ec2Options,
        ILogger<Ec2Repository> logger)
    {
        _ec2Client = ec2Client;
        _logger = logger;
        _publicInboundCidrs = ec2Options.Value.PublicInboundCidrs
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Ec2OpenPortsResponseDto> GetInstancesWithOpenPortsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        string normalizedRegion = NormalizeRegion(region);
        IAmazonEC2 client = GetRegionalClient(normalizedRegion);

        List<Instance> instances = await ListInstancesAsync(client, cancellationToken);
        Dictionary<string, SecurityGroup> securityGroups = await LoadSecurityGroupsAsync(client, instances, cancellationToken);

        _logger.LogInformation(
            "Found {InstanceCount} EC2 instances in region {Region}, analyzing security group inbound rules",
            instances.Count,
            normalizedRegion);

        List<Ec2InstancePortsDto> instanceResults = instances
            .Select(instance => MapInstance(instance, securityGroups))
            .OrderBy(i => i.InstanceId, StringComparer.Ordinal)
            .ToList();

        return new Ec2OpenPortsResponseDto
        {
            Region = normalizedRegion,
            TotalInstances = instanceResults.Count,
            InstancesWithPublicPorts = instanceResults.Count(i => i.HasPubliclyExposedPorts),
            Instances = instanceResults,
            ScannedAt = DateTime.UtcNow
        };
    }

    public async Task<Ec2UnusedSecurityGroupsResponseDto> GetUnusedSecurityGroupsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        string normalizedRegion = NormalizeRegion(region);
        IAmazonEC2 client = GetRegionalClient(normalizedRegion);

        List<SecurityGroup> allSecurityGroups = await ListAllSecurityGroupsAsync(client, cancellationToken);
        HashSet<string> securityGroupsInUse = await ListSecurityGroupIdsAttachedToEnisAsync(client, cancellationToken);

        List<Ec2UnusedSecurityGroupDto> unusedSecurityGroups = allSecurityGroups
            .Where(group => !securityGroupsInUse.Contains(group.GroupId))
            .Select(MapUnusedSecurityGroup)
            .OrderBy(group => group.GroupName, StringComparer.Ordinal)
            .ThenBy(group => group.GroupId, StringComparer.Ordinal)
            .ToList();

        _logger.LogInformation(
            "Found {UnusedCount}/{TotalCount} unused security groups in region {Region}",
            unusedSecurityGroups.Count,
            allSecurityGroups.Count,
            normalizedRegion);

        return new Ec2UnusedSecurityGroupsResponseDto
        {
            Region = normalizedRegion,
            TotalSecurityGroups = allSecurityGroups.Count,
            UnusedSecurityGroupsCount = unusedSecurityGroups.Count,
            SecurityGroups = unusedSecurityGroups,
            ScannedAt = DateTime.UtcNow
        };
    }

    public async Task<Ec2UnattachedVolumesResponseDto> GetUnattachedVolumesByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        string normalizedRegion = NormalizeRegion(region);
        IAmazonEC2 client = GetRegionalClient(normalizedRegion);

        List<Volume> allVolumes = await ListAllVolumesAsync(client, cancellationToken);
        List<Ec2UnattachedVolumeDto> unattachedVolumes = allVolumes
            .Where(IsUnattached)
            .Select(MapUnattachedVolume)
            .OrderBy(volume => volume.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(volume => volume.VolumeId, StringComparer.Ordinal)
            .ToList();

        _logger.LogInformation(
            "Found {UnattachedCount}/{TotalCount} unattached EBS volumes in region {Region}",
            unattachedVolumes.Count,
            allVolumes.Count,
            normalizedRegion);

        return new Ec2UnattachedVolumesResponseDto
        {
            Region = normalizedRegion,
            TotalVolumes = allVolumes.Count,
            UnattachedVolumesCount = unattachedVolumes.Count,
            TotalUnattachedSizeGiB = unattachedVolumes.Sum(volume => volume.SizeGiB),
            Volumes = unattachedVolumes,
            ScannedAt = DateTime.UtcNow
        };
    }

    public async Task<Ec2Imdsv1InstancesResponseDto> GetImdsv1InstancesByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        string normalizedRegion = NormalizeRegion(region);
        IAmazonEC2 client = GetRegionalClient(normalizedRegion);

        List<Instance> instances = await ListInstancesAsync(client, cancellationToken);
        List<Ec2Imdsv1InstanceDto> imdsv1Instances = instances
            .Where(IsRelevantInstanceState)
            .Where(AllowsImdsv1)
            .Select(MapImdsv1Instance)
            .OrderBy(instance => instance.InstanceId, StringComparer.Ordinal)
            .ToList();

        int relevantInstanceCount = instances.Count(IsRelevantInstanceState);

        _logger.LogInformation(
            "Found {Imdsv1Count}/{TotalCount} EC2 instances allowing IMDSv1 in region {Region}",
            imdsv1Instances.Count,
            relevantInstanceCount,
            normalizedRegion);

        return new Ec2Imdsv1InstancesResponseDto
        {
            Region = normalizedRegion,
            TotalInstances = relevantInstanceCount,
            Imdsv1InstancesCount = imdsv1Instances.Count,
            Instances = imdsv1Instances,
            ScannedAt = DateTime.UtcNow
        };
    }

    private static bool IsRelevantInstanceState(Instance instance)
    {
        string? state = instance.State?.Name?.Value;
        return !string.Equals(state, "terminated", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(state, "shutting-down", StringComparison.OrdinalIgnoreCase);
    }

    private static bool AllowsImdsv1(Instance instance)
    {
        InstanceMetadataOptionsResponse? metadataOptions = instance.MetadataOptions;
        if (metadataOptions is null)
            return true;

        string? httpEndpoint = metadataOptions.HttpEndpoint?.Value;
        if (string.Equals(httpEndpoint, "disabled", StringComparison.OrdinalIgnoreCase))
            return false;

        string? httpTokens = metadataOptions.HttpTokens?.Value;
        return !string.Equals(httpTokens, "required", StringComparison.OrdinalIgnoreCase);
    }

    private static Ec2Imdsv1InstanceDto MapImdsv1Instance(Instance instance)
    {
        string? httpTokens = instance.MetadataOptions?.HttpTokens?.Value;
        string? httpEndpoint = instance.MetadataOptions?.HttpEndpoint?.Value;

        return new Ec2Imdsv1InstanceDto
        {
            InstanceId = instance.InstanceId,
            Name = instance.Tags?
                .FirstOrDefault(tag => string.Equals(tag.Key, "Name", StringComparison.OrdinalIgnoreCase))
                ?.Value,
            State = instance.State?.Name?.Value,
            InstanceType = instance.InstanceType?.Value,
            HttpTokens = string.IsNullOrWhiteSpace(httpTokens) ? "optional" : httpTokens,
            HttpEndpoint = string.IsNullOrWhiteSpace(httpEndpoint) ? "enabled" : httpEndpoint,
            Recommendation = BuildImdsv1Recommendation(httpTokens)
        };
    }

    private static string BuildImdsv1Recommendation(string? httpTokens) =>
        string.Equals(httpTokens, "optional", StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(httpTokens)
            ? "Require IMDSv2 by setting HttpTokens to required. This reduces SSRF and metadata credential theft risk."
            : "Review instance metadata options and enforce IMDSv2 unless a legacy integration requires IMDSv1.";

    private static async Task<List<Volume>> ListAllVolumesAsync(
        IAmazonEC2 client,
        CancellationToken cancellationToken)
    {
        List<Volume> volumes = new();
        string? nextToken = null;

        do
        {
            DescribeVolumesResponse response = await client.DescribeVolumesAsync(
                new DescribeVolumesRequest { NextToken = nextToken },
                cancellationToken);

            if (response.Volumes is not null)
                volumes.AddRange(response.Volumes);

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        return volumes;
    }

    private static bool IsUnattached(Volume volume) =>
        volume.Attachments is null ||
        volume.Attachments.Count == 0 ||
        !volume.Attachments.Any(attachment =>
            string.Equals(attachment.State, VolumeAttachmentState.Attached, StringComparison.OrdinalIgnoreCase));

    private static Ec2UnattachedVolumeDto MapUnattachedVolume(Volume volume)
    {
        string? name = volume.Tags?
            .FirstOrDefault(tag => string.Equals(tag.Key, "Name", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        bool encrypted = volume.Encrypted == true;

        return new Ec2UnattachedVolumeDto
        {
            VolumeId = volume.VolumeId,
            Name = name,
            SizeGiB = volume.Size ?? 0,
            VolumeType = volume.VolumeType?.Value,
            State = volume.State?.Value,
            Encrypted = encrypted,
            SnapshotId = volume.SnapshotId,
            AvailabilityZone = volume.AvailabilityZone,
            CreatedAt = volume.CreateTime,
            Recommendation = BuildUnattachedVolumeRecommendation(encrypted)
        };
    }

    private static string BuildUnattachedVolumeRecommendation(bool encrypted)
    {
        if (!encrypted)
            return "EBS volume is not attached to any instance and is not encrypted; create a snapshot if needed, enable encryption on replacement volumes, then delete to reduce cost.";

        return "EBS volume is not attached to any instance; create a snapshot if needed, then delete to reduce cost.";
    }

    private async Task<List<SecurityGroup>> ListAllSecurityGroupsAsync(
        IAmazonEC2 client,
        CancellationToken cancellationToken)
    {
        List<SecurityGroup> securityGroups = new();
        string? nextToken = null;

        do
        {
            DescribeSecurityGroupsResponse response = await client.DescribeSecurityGroupsAsync(
                new DescribeSecurityGroupsRequest { NextToken = nextToken },
                cancellationToken);

            if (response.SecurityGroups is not null)
                securityGroups.AddRange(response.SecurityGroups);

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        return securityGroups;
    }

    private static async Task<HashSet<string>> ListSecurityGroupIdsAttachedToEnisAsync(
        IAmazonEC2 client,
        CancellationToken cancellationToken)
    {
        HashSet<string> securityGroupsInUse = new(StringComparer.Ordinal);
        string? nextToken = null;

        do
        {
            DescribeNetworkInterfacesResponse response = await client.DescribeNetworkInterfacesAsync(
                new DescribeNetworkInterfacesRequest { NextToken = nextToken },
                cancellationToken);

            foreach (NetworkInterface networkInterface in response.NetworkInterfaces ?? [])
            {
                foreach (GroupIdentifier group in networkInterface.Groups ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(group.GroupId))
                        securityGroupsInUse.Add(group.GroupId);
                }
            }

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        return securityGroupsInUse;
    }

    private static Ec2UnusedSecurityGroupDto MapUnusedSecurityGroup(SecurityGroup securityGroup)
    {
        bool isDefault = string.Equals(securityGroup.GroupName, "default", StringComparison.OrdinalIgnoreCase);

        return new Ec2UnusedSecurityGroupDto
        {
            GroupId = securityGroup.GroupId,
            GroupName = securityGroup.GroupName,
            Description = securityGroup.Description,
            VpcId = securityGroup.VpcId,
            IsDefaultSecurityGroup = isDefault,
            InboundRuleCount = securityGroup.IpPermissions?.Count ?? 0,
            OutboundRuleCount = securityGroup.IpPermissionsEgress?.Count ?? 0,
            Recommendation = BuildUnusedSecurityGroupRecommendation(isDefault)
        };
    }

    private static string BuildUnusedSecurityGroupRecommendation(bool isDefaultSecurityGroup) =>
        isDefaultSecurityGroup
            ? "Default VPC security group; it cannot be deleted. Tighten inbound and outbound rules if no ENI uses it."
            : "No ENI references this security group; review dependencies and delete if it is no longer needed.";

    private async Task<List<Instance>> ListInstancesAsync(
        IAmazonEC2 client,
        CancellationToken cancellationToken)
    {
        List<Instance> instances = new();
        string? nextToken = null;

        do
        {
            DescribeInstancesResponse response = await client.DescribeInstancesAsync(
                new DescribeInstancesRequest { NextToken = nextToken },
                cancellationToken);

            foreach (var reservation in response.Reservations)
                instances.AddRange(reservation.Instances);

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        return instances;
    }

    private async Task<Dictionary<string, SecurityGroup>> LoadSecurityGroupsAsync(
        IAmazonEC2 client,
        IReadOnlyList<Instance> instances,
        CancellationToken cancellationToken)
    {
        List<string> groupIds = instances
            .SelectMany(i => i.SecurityGroups)
            .Select(g => g.GroupId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return await LoadSecurityGroupsByIdsAsync(client, groupIds, cancellationToken);
    }

    internal static async Task<Dictionary<string, SecurityGroup>> LoadSecurityGroupsByIdsAsync(
        IAmazonEC2 client,
        IReadOnlyList<string> groupIds,
        CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0)
            return new Dictionary<string, SecurityGroup>(StringComparer.Ordinal);

        Dictionary<string, SecurityGroup> securityGroups = new(StringComparer.Ordinal);

        foreach (var batch in groupIds.Chunk(SecurityGroupBatchSize))
        {
            DescribeSecurityGroupsResponse response = await client.DescribeSecurityGroupsAsync(
                new DescribeSecurityGroupsRequest { GroupIds = batch.ToList() },
                cancellationToken);

            foreach (var group in response.SecurityGroups)
                securityGroups[group.GroupId] = group;
        }

        return securityGroups;
    }

    private Ec2InstancePortsDto MapInstance(
        Instance instance,
        IReadOnlyDictionary<string, SecurityGroup> securityGroups)
    {
        List<SecurityGroupDto> mappedGroups = instance.SecurityGroups
            .Where(group => !string.IsNullOrWhiteSpace(group.GroupId))
            .Select(group =>
            {
                securityGroups.TryGetValue(group.GroupId, out var securityGroup);
                return SecurityGroupRuleMapper.MapSecurityGroup(
                    group.GroupId,
                    group.GroupName,
                    securityGroup,
                    _publicInboundCidrs);
            })
            .ToList();

        List<InboundRuleDto> publicRules = mappedGroups
            .SelectMany(g => g.InboundRules)
            .Where(r => r.IsPubliclyExposed)
            .ToList();

        return new Ec2InstancePortsDto
        {
            InstanceId = instance.InstanceId,
            Name = instance.Tags.FirstOrDefault(t => t.Key == "Name")?.Value,
            State = instance.State?.Name?.Value ?? "unknown",
            InstanceType = instance.InstanceType?.Value,
            PrivateIpAddress = instance.PrivateIpAddress,
            PublicIpAddress = instance.PublicIpAddress,
            SecurityGroups = mappedGroups,
            HasPubliclyExposedPorts = publicRules.Count > 0,
            PubliclyExposedInboundRules = publicRules,
            Recommendation = SecurityGroupRuleMapper.BuildEc2Recommendation(publicRules)
        };
    }

    private IAmazonEC2 GetRegionalClient(string region)
    {
        if (string.Equals(_ec2Client.Config.RegionEndpoint?.SystemName, region, StringComparison.OrdinalIgnoreCase))
            return _ec2Client;

        return new AmazonEC2Client(RegionEndpoint.GetBySystemName(region));
    }

    private static string NormalizeRegion(string region) =>
        string.IsNullOrWhiteSpace(region) ? "us-east-1" : region;
}
