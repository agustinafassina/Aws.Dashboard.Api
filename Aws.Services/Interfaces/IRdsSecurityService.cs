using Aws.Models.Dto;

namespace Aws.Services.Interfaces;

public interface IRdsSecurityService
{
    Task<RdsOpenPortsResponseDto> GetInstancesWithOpenPortsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);

    Task<RdsUnencryptedInstancesResponseDto> GetUnencryptedInstancesByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);
}
