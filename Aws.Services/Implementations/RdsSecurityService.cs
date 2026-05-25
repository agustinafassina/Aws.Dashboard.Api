using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;
using Aws.Services.Interfaces;

namespace Aws.Services.Implementations;

public class RdsSecurityService : IRdsSecurityService
{
    private readonly IRdsRepository _rdsRepository;
    private readonly ILogger<RdsSecurityService> _logger;

    public RdsSecurityService(IRdsRepository rdsRepository, ILogger<RdsSecurityService> logger)
    {
        _rdsRepository = rdsRepository;
        _logger = logger;
    }

    public async Task<RdsOpenPortsResponseDto> GetInstancesWithOpenPortsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required.", nameof(region));

        _logger.LogInformation("Scanning RDS instances and security groups in region {Region}", region);
        RdsOpenPortsResponseDto result = await _rdsRepository.GetInstancesWithOpenPortsByRegionAsync(region, cancellationToken);

        _logger.LogInformation(
            "Scan complete for region {Region}: {PublicCount}/{TotalCount} RDS instances have public exposure risks",
            result.Region,
            result.InstancesWithPublicPorts,
            result.TotalInstances);

        return result;
    }
}
