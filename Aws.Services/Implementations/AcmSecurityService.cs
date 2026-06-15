using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;
using Aws.Services.Interfaces;

namespace Aws.Services.Implementations;

public class AcmSecurityService : IAcmSecurityService
{
    private const int MinExpirationWindowDays = 1;
    private const int MaxExpirationWindowDays = 365;

    private readonly IAcmRepository _acmRepository;
    private readonly ILogger<AcmSecurityService> _logger;

    public AcmSecurityService(IAcmRepository acmRepository, ILogger<AcmSecurityService> logger)
    {
        _acmRepository = acmRepository;
        _logger = logger;
    }

    public async Task<AcmExpiringCertificatesResponseDto> GetExpiringCertificatesByRegionAsync(
        string region,
        int expirationWindowDays,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required.", nameof(region));

        if (expirationWindowDays < MinExpirationWindowDays || expirationWindowDays > MaxExpirationWindowDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expirationWindowDays),
                expirationWindowDays,
                $"Days must be between {MinExpirationWindowDays} and {MaxExpirationWindowDays}.");
        }

        _logger.LogInformation(
            "Scanning ACM certificates expiring within {Days} days in region {Region}",
            expirationWindowDays,
            region);

        AcmExpiringCertificatesResponseDto result = await _acmRepository.GetExpiringCertificatesByRegionAsync(
            region,
            expirationWindowDays,
            cancellationToken);

        _logger.LogInformation(
            "ACM scan complete for region {Region}: {ExpiredCount} expired, {ExpiringCount} expiring within {Days} days",
            result.Region,
            result.ExpiredCertificatesCount,
            result.ExpiringCertificatesCount,
            result.ExpirationWindowDays);

        return result;
    }
}
