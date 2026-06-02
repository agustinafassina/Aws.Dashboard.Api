using Aws.Models.Dto;

namespace Aws.Services.Interfaces;

public interface IAuditService
{
    Task<UntaggedResourcesResponseDto> GetUntaggedResourcesByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);
}
