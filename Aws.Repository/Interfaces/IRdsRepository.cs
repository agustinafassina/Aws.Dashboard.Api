using Aws.Models.Dto;

namespace Aws.Repository.Interfaces;

public interface IRdsRepository
{
    Task<RdsOpenPortsResponseDto> GetInstancesWithOpenPortsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);

    Task<RdsUnencryptedInstancesResponseDto> GetUnencryptedInstancesByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);

    Task<RdsNoBackupsResponseDto> GetInstancesWithoutAdequateBackupsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);
}
