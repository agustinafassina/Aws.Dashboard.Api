using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;
using Aws.Services.Interfaces;

namespace Aws.Services.Implementations;

public class AuditService : IAuditService
{
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IAuditRepository auditRepository, ILogger<AuditService> logger)
    {
        _auditRepository = auditRepository;
        _logger = logger;
    }

    public async Task<UntaggedResourcesResponseDto> GetUntaggedResourcesByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required.", nameof(region));

        _logger.LogInformation("Auditing untagged resources in region {Region}", region);
        return await _auditRepository.GetUntaggedResourcesByRegionAsync(region, cancellationToken);
    }

    public async Task<ResourcesByProjectTagResponseDto> GetResourcesByProjectTagAsync(
        string region,
        string projectTagValue,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required.", nameof(region));

        if (string.IsNullOrWhiteSpace(projectTagValue))
            throw new ArgumentException("Project tag value is required.", nameof(projectTagValue));

        _logger.LogInformation(
            "Searching resources by project tag {TagValue} in region {Region}",
            projectTagValue,
            region);

        return await _auditRepository.GetResourcesByProjectTagAsync(
            region,
            projectTagValue.Trim(),
            cancellationToken);
    }
}
