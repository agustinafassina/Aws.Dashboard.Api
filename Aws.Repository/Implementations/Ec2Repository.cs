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
