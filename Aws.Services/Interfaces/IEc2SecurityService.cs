using Aws.Models.Dto;

namespace Aws.Services.Interfaces;

public interface IEc2SecurityService
{
    Task<Ec2OpenPortsResponseDto> GetInstancesWithOpenPortsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);

    Task<Ec2UnusedSecurityGroupsResponseDto> GetUnusedSecurityGroupsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);
}
