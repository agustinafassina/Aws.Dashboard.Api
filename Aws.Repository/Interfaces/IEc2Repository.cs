using Aws.Models.Dto;

namespace Aws.Repository.Interfaces;

public interface IEc2Repository
{
    Task<Ec2OpenPortsResponseDto> GetInstancesWithOpenPortsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);

    Task<Ec2UnusedSecurityGroupsResponseDto> GetUnusedSecurityGroupsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);

    Task<Ec2UnattachedVolumesResponseDto> GetUnattachedVolumesByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);
}
