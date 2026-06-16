using Aws.Models.Dto;

namespace Aws.Repository.Interfaces;

public interface IEcrRepository
{
    Task<EcrRepositoryRisksResponseDto> GetRepositoryRisksByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);
}
