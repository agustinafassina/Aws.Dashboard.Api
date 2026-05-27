using Aws.Models.Dto;

namespace Aws.Services.Interfaces;

public interface IInspectorSecurityService
{
    Task<InspectorVulnerabilitiesResponseDto> GetVulnerabilitiesAsync(
        string region,
        string? resourceType,
        string? severity,
        string? status,
        CancellationToken cancellationToken = default);
}
