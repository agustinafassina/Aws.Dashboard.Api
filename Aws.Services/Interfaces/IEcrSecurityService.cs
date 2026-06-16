using Aws.Models.Dto;

namespace Aws.Services.Interfaces;

public interface IEcrSecurityService
{
    Task<EcrRepositoryRisksResponseDto> GetRepositoryRisksByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);
}
