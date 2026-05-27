using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;
using Aws.Services.Interfaces;

namespace Aws.Services.Implementations;

public class InspectorSecurityService : IInspectorSecurityService
{
    private static readonly HashSet<string> AllowedResourceTypes =
        new(StringComparer.OrdinalIgnoreCase) { "ec2", "lambda", "ecr" };

    private static readonly HashSet<string> AllowedSeverities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "INFORMATIONAL", "LOW", "MEDIUM", "HIGH", "CRITICAL", "UNTRIAGED"
        };

    private static readonly HashSet<string> AllowedStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "ACTIVE", "SUPPRESSED", "CLOSED" };

    private readonly IInspectorRepository _inspectorRepository;
    private readonly ILogger<InspectorSecurityService> _logger;

    public InspectorSecurityService(
        IInspectorRepository inspectorRepository,
        ILogger<InspectorSecurityService> logger)
    {
        _inspectorRepository = inspectorRepository;
        _logger = logger;
    }

    public async Task<InspectorVulnerabilitiesResponseDto> GetVulnerabilitiesAsync(
        string region,
        string? resourceType,
        string? severity,
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required.", nameof(region));

        if (!string.IsNullOrWhiteSpace(resourceType) && !AllowedResourceTypes.Contains(resourceType))
            throw new ArgumentException("resourceType must be one of: ec2, lambda, ecr.", nameof(resourceType));

        if (!string.IsNullOrWhiteSpace(severity) && !AllowedSeverities.Contains(severity))
            throw new ArgumentException(
                "severity must be one of: INFORMATIONAL, LOW, MEDIUM, HIGH, CRITICAL, UNTRIAGED.",
                nameof(severity));

        if (!string.IsNullOrWhiteSpace(status) && !AllowedStatuses.Contains(status))
            throw new ArgumentException("status must be one of: ACTIVE, SUPPRESSED, CLOSED.", nameof(status));

        _logger.LogInformation(
            "Scanning Inspector vulnerabilities in region {Region} (resourceType={ResourceType}, severity={Severity}, status={Status})",
            region,
            resourceType ?? "all",
            severity ?? "all",
            status ?? "all");

        InspectorVulnerabilitiesResponseDto result = await _inspectorRepository.GetVulnerabilitiesAsync(
            region,
            resourceType,
            severity,
            status,
            cancellationToken);

        _logger.LogInformation(
            "Inspector scan complete for region {Region}: {FindingCount} findings returned",
            result.Region,
            result.TotalFindings);

        return result;
    }
}
