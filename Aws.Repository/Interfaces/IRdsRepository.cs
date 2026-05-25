using Aws.Models.Dto;

namespace Aws.Repository.Interfaces;

public interface IRdsRepository
{
    Task<RdsOpenPortsResponseDto> GetInstancesWithOpenPortsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);
}
