using Amazon;
using Amazon.EC2;
using Amazon.RDS;
using Amazon.RDS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aws.Models.Configuration;
using Aws.Models.Dto;
using Aws.Repository.Helpers;
using Aws.Repository.Interfaces;
using Amazon.EC2.Model;

namespace Aws.Repository.Implementations;

public class RdsRepository : IRdsRepository
{
    private readonly IAmazonRDS _rdsClient;
    private readonly IAmazonEC2 _ec2Client;
    private readonly ILogger<RdsRepository> _logger;
    private readonly HashSet<string> _publicInboundCidrs;

    public RdsRepository(
        IAmazonRDS rdsClient,
        IAmazonEC2 ec2Client,
        IOptions<RdsOptions> rdsOptions,
        ILogger<RdsRepository> logger)
    {
        _rdsClient = rdsClient;
        _ec2Client = ec2Client;
        _logger = logger;
        _publicInboundCidrs = rdsOptions.Value.PublicInboundCidrs
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<RdsOpenPortsResponseDto> GetInstancesWithOpenPortsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        string normalizedRegion = NormalizeRegion(region);
        IAmazonRDS rdsClient = GetRegionalRdsClient(normalizedRegion);
        IAmazonEC2 ec2Client = GetRegionalEc2Client(normalizedRegion);

        List<DBInstance> instances = await ListDbInstancesAsync(rdsClient, cancellationToken);
        List<string> groupIds = instances
            .SelectMany(i => i.VpcSecurityGroups ?? [])
            .Select(g => g.VpcSecurityGroupId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Dictionary<string, SecurityGroup> securityGroups = await Ec2Repository.LoadSecurityGroupsByIdsAsync(
            ec2Client,
            groupIds,
            cancellationToken);

        _logger.LogInformation(
            "Found {InstanceCount} RDS instances in region {Region}, analyzing security group inbound rules",
            instances.Count,
            normalizedRegion);

        List<RdsInstancePortsDto> instanceResults = instances
            .Select(instance => MapInstance(instance, securityGroups))
            .OrderBy(i => i.DbInstanceIdentifier, StringComparer.Ordinal)
            .ToList();

        return new RdsOpenPortsResponseDto
        {
            Region = normalizedRegion,
            TotalInstances = instanceResults.Count,
            InstancesWithPublicPorts = instanceResults.Count(i => i.HasPubliclyExposedPorts),
            Instances = instanceResults,
            ScannedAt = DateTime.UtcNow
        };
    }

    private async Task<List<DBInstance>> ListDbInstancesAsync(
        IAmazonRDS client,
        CancellationToken cancellationToken)
    {
        List<DBInstance> instances = new();
        string? marker = null;

        do
        {
            DescribeDBInstancesResponse response = await client.DescribeDBInstancesAsync(
                new DescribeDBInstancesRequest { Marker = marker },
                cancellationToken);

            instances.AddRange(response.DBInstances ?? []);
            marker = response.Marker;
        }
        while (!string.IsNullOrEmpty(marker));

        return instances;
    }

    private RdsInstancePortsDto MapInstance(
        DBInstance instance,
        IReadOnlyDictionary<string, Amazon.EC2.Model.SecurityGroup> securityGroups)
    {
        List<SecurityGroupDto> mappedGroups = (instance.VpcSecurityGroups ?? [])
            .Where(group => !string.IsNullOrWhiteSpace(group.VpcSecurityGroupId))
            .Select(group =>
            {
                securityGroups.TryGetValue(group.VpcSecurityGroupId, out var securityGroup);
                return SecurityGroupRuleMapper.MapSecurityGroup(
                    group.VpcSecurityGroupId,
                    group.VpcSecurityGroupId,
                    securityGroup,
                    _publicInboundCidrs);
            })
            .ToList();

        List<InboundRuleDto> publicRules = mappedGroups
            .SelectMany(g => g.InboundRules)
            .Where(r => r.IsPubliclyExposed)
            .ToList();

        bool publiclyAccessible = instance.PubliclyAccessible == true;
        int? dbPort = instance.Endpoint?.Port;

        return new RdsInstancePortsDto
        {
            DbInstanceIdentifier = instance.DBInstanceIdentifier ?? "unknown",
            Engine = instance.Engine ?? "unknown",
            EngineVersion = instance.EngineVersion,
            Status = instance.DBInstanceStatus ?? "unknown",
            Endpoint = instance.Endpoint?.Address,
            Port = dbPort,
            PubliclyAccessible = publiclyAccessible,
            SecurityGroups = mappedGroups,
            HasPubliclyExposedPorts = publicRules.Count > 0 || publiclyAccessible,
            PubliclyExposedInboundRules = publicRules,
            Recommendation = SecurityGroupRuleMapper.BuildRdsRecommendation(
                publiclyAccessible,
                instance.Engine,
                dbPort,
                publicRules)
        };
    }

    private IAmazonRDS GetRegionalRdsClient(string region)
    {
        if (string.Equals(_rdsClient.Config.RegionEndpoint?.SystemName, region, StringComparison.OrdinalIgnoreCase))
            return _rdsClient;

        return new AmazonRDSClient(RegionEndpoint.GetBySystemName(region));
    }

    private IAmazonEC2 GetRegionalEc2Client(string region)
    {
        if (string.Equals(_ec2Client.Config.RegionEndpoint?.SystemName, region, StringComparison.OrdinalIgnoreCase))
            return _ec2Client;

        return new AmazonEC2Client(RegionEndpoint.GetBySystemName(region));
    }

    private static string NormalizeRegion(string region) =>
        string.IsNullOrWhiteSpace(region) ? "us-east-1" : region;
}
