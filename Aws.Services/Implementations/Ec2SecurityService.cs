using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;
using Aws.Services.Interfaces;

namespace Aws.Services.Implementations;

public class Ec2SecurityService : IEc2SecurityService
{
    private readonly IEc2Repository _ec2Repository;
    private readonly ILogger<Ec2SecurityService> _logger;

    public Ec2SecurityService(IEc2Repository ec2Repository, ILogger<Ec2SecurityService> logger)
    {
        _ec2Repository = ec2Repository;
        _logger = logger;
    }

    public async Task<Ec2OpenPortsResponseDto> GetInstancesWithOpenPortsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required.", nameof(region));

        _logger.LogInformation("Scanning EC2 instances and security groups in region {Region}", region);
        Ec2OpenPortsResponseDto result = await _ec2Repository.GetInstancesWithOpenPortsByRegionAsync(region, cancellationToken);

        _logger.LogInformation(
            "Scan complete for region {Region}: {PublicCount}/{TotalCount} instances have publicly exposed inbound ports",
            result.Region,
            result.InstancesWithPublicPorts,
            result.TotalInstances);

        return result;
    }

    public async Task<Ec2UnusedSecurityGroupsResponseDto> GetUnusedSecurityGroupsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required.", nameof(region));

        _logger.LogInformation("Scanning unused EC2 security groups in region {Region}", region);
        Ec2UnusedSecurityGroupsResponseDto result =
            await _ec2Repository.GetUnusedSecurityGroupsByRegionAsync(region, cancellationToken);

        _logger.LogInformation(
            "Unused security group scan complete for region {Region}: {UnusedCount}/{TotalCount} without ENI attachments",
            result.Region,
            result.UnusedSecurityGroupsCount,
            result.TotalSecurityGroups);

        return result;
    }

    public async Task<Ec2UnattachedVolumesResponseDto> GetUnattachedVolumesByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required.", nameof(region));

        _logger.LogInformation("Scanning unattached EBS volumes in region {Region}", region);
        Ec2UnattachedVolumesResponseDto result =
            await _ec2Repository.GetUnattachedVolumesByRegionAsync(region, cancellationToken);

        _logger.LogInformation(
            "Unattached volume scan complete for region {Region}: {UnattachedCount}/{TotalCount} volumes ({TotalGiB} GiB unattached)",
            result.Region,
            result.UnattachedVolumesCount,
            result.TotalVolumes,
            result.TotalUnattachedSizeGiB);

        return result;
    }
}
