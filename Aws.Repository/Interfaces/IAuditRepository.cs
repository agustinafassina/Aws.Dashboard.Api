using Aws.Models.Dto;

namespace Aws.Repository.Interfaces;

public interface IAuditRepository
{
    Task<UntaggedResourcesResponseDto> GetUntaggedResourcesByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);

    Task<ResourcesByProjectTagResponseDto> GetResourcesByProjectTagAsync(
        string region,
        string projectTagValue,
        CancellationToken cancellationToken = default);
}
