using Aws.Models.Dto;

namespace Aws.Repository.Interfaces;

public interface IInspectorRepository
{
    Task<InspectorVulnerabilitiesResponseDto> GetVulnerabilitiesAsync(
        string region,
        string? resourceType,
        string? severity,
        string? status,
        CancellationToken cancellationToken = default);
}
