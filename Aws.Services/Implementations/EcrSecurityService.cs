using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;
using Aws.Services.Interfaces;

namespace Aws.Services.Implementations;

public class EcrSecurityService : IEcrSecurityService
{
    private readonly IEcrRepository _ecrRepository;
    private readonly ILogger<EcrSecurityService> _logger;

    public EcrSecurityService(IEcrRepository ecrRepository, ILogger<EcrSecurityService> logger)
    {
        _ecrRepository = ecrRepository;
        _logger = logger;
    }

    public async Task<EcrRepositoryRisksResponseDto> GetRepositoryRisksByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required.", nameof(region));

        _logger.LogInformation("Scanning ECR repository risks in region {Region}", region);

        EcrRepositoryRisksResponseDto result =
            await _ecrRepository.GetRepositoryRisksByRegionAsync(region, cancellationToken);

        _logger.LogInformation(
            "ECR scan complete for region {Region}: {RiskCount}/{TotalCount} repositories at risk",
            result.Region,
            result.RepositoriesAtRiskCount,
            result.TotalRepositoriesScanned);

        return result;
    }
}
