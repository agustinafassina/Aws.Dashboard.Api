using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;
using Aws.Services.Interfaces;

namespace Aws.Services.Implementations;

public class ElbSecurityService : IElbSecurityService
{
    private readonly IElbRepository _elbRepository;
    private readonly ILogger<ElbSecurityService> _logger;

    public ElbSecurityService(IElbRepository elbRepository, ILogger<ElbSecurityService> logger)
    {
        _elbRepository = elbRepository;
        _logger = logger;
    }

    public async Task<ElbPublicListenersResponseDto> GetPublicListenersByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required.", nameof(region));

        _logger.LogInformation("Scanning internet-facing load balancers in region {Region}", region);

        ElbPublicListenersResponseDto result =
            await _elbRepository.GetPublicListenersByRegionAsync(region, cancellationToken);

        _logger.LogInformation(
            "ELB scan complete for region {Region}: {PublicCount}/{TotalCount} load balancers flagged",
            result.Region,
            result.PublicLoadBalancersCount,
            result.TotalLoadBalancersScanned);

        return result;
    }
}
