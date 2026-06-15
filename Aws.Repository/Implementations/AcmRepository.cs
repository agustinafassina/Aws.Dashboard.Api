using Amazon;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;

namespace Aws.Repository.Implementations;

public class AcmRepository : IAcmRepository
{
    private readonly IAmazonCertificateManager _acmClient;
    private readonly ILogger<AcmRepository> _logger;

    public AcmRepository(IAmazonCertificateManager acmClient, ILogger<AcmRepository> logger)
    {
        _acmClient = acmClient;
        _logger = logger;
    }

    public async Task<AcmExpiringCertificatesResponseDto> GetExpiringCertificatesByRegionAsync(
        string region,
        int expirationWindowDays,
        CancellationToken cancellationToken = default)
    {
        string normalizedRegion = NormalizeRegion(region);
        IAmazonCertificateManager client = GetRegionalClient(normalizedRegion);
        DateTime utcNow = DateTime.UtcNow;
        DateTime expirationThreshold = utcNow.AddDays(expirationWindowDays);

        List<CertificateSummary> certificates = await ListCertificatesAsync(client, cancellationToken);
        List<AcmExpiringCertificateDto> matchingCertificates = new();

        foreach (CertificateSummary certificate in certificates)
        {
            if (string.IsNullOrWhiteSpace(certificate.CertificateArn) ||
                string.IsNullOrWhiteSpace(certificate.DomainName) ||
                certificate.NotAfter is null)
            {
                continue;
            }

            DateTime notAfterUtc = certificate.NotAfter.Value.ToUniversalTime();
            if (notAfterUtc > expirationThreshold)
                continue;

            int daysUntilExpiry = (int)Math.Floor((notAfterUtc - utcNow).TotalDays);
            bool isExpired = daysUntilExpiry < 0;

            matchingCertificates.Add(new AcmExpiringCertificateDto
            {
                CertificateArn = certificate.CertificateArn,
                DomainName = certificate.DomainName,
                SubjectAlternativeNames = certificate.SubjectAlternativeNameSummaries ?? [],
                Status = certificate.Status?.Value,
                Type = certificate.Type?.Value,
                InUse = certificate.InUse == true,
                NotBefore = certificate.NotBefore,
                NotAfter = certificate.NotAfter,
                DaysUntilExpiry = daysUntilExpiry,
                IsExpired = isExpired,
                Recommendation = BuildRecommendation(isExpired, daysUntilExpiry, certificate.InUse == true)
            });
        }

        List<AcmExpiringCertificateDto> orderedCertificates = matchingCertificates
            .OrderBy(certificate => certificate.NotAfter)
            .ThenBy(certificate => certificate.DomainName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation(
            "Found {MatchCount}/{TotalCount} ACM certificates expiring within {Days} days in region {Region}",
            orderedCertificates.Count,
            certificates.Count,
            expirationWindowDays,
            normalizedRegion);

        return new AcmExpiringCertificatesResponseDto
        {
            Region = normalizedRegion,
            ExpirationWindowDays = expirationWindowDays,
            TotalCertificatesScanned = certificates.Count,
            ExpiringCertificatesCount = orderedCertificates.Count(certificate => !certificate.IsExpired),
            ExpiredCertificatesCount = orderedCertificates.Count(certificate => certificate.IsExpired),
            Certificates = orderedCertificates,
            ScannedAt = utcNow
        };
    }

    private static async Task<List<CertificateSummary>> ListCertificatesAsync(
        IAmazonCertificateManager client,
        CancellationToken cancellationToken)
    {
        List<CertificateSummary> certificates = new();
        string? nextToken = null;

        do
        {
            ListCertificatesResponse response = await client.ListCertificatesAsync(
                new ListCertificatesRequest
                {
                    CertificateStatuses =
                    [
                        CertificateStatus.ISSUED,
                        CertificateStatus.EXPIRED,
                        CertificateStatus.INACTIVE,
                        CertificateStatus.PENDING_VALIDATION,
                        CertificateStatus.VALIDATION_TIMED_OUT
                    ],
                    NextToken = nextToken
                },
                cancellationToken);

            if (response.CertificateSummaryList is not null)
                certificates.AddRange(response.CertificateSummaryList);

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        return certificates;
    }

    private IAmazonCertificateManager GetRegionalClient(string region)
    {
        if (string.Equals(_acmClient.Config.RegionEndpoint?.SystemName, region, StringComparison.OrdinalIgnoreCase))
            return _acmClient;

        return new AmazonCertificateManagerClient(RegionEndpoint.GetBySystemName(region));
    }

    private static string NormalizeRegion(string region) =>
        string.IsNullOrWhiteSpace(region) ? "us-east-1" : region;

    private static string BuildRecommendation(bool isExpired, int daysUntilExpiry, bool inUse)
    {
        if (isExpired)
        {
            return inUse
                ? "Certificate is expired and still marked in use; renew or replace it immediately to avoid service disruption."
                : "Certificate is expired; remove it or renew if it is still required.";
        }

        if (daysUntilExpiry <= 7)
        {
            return inUse
                ? "Certificate expires within 7 days and is in use; renew or replace it immediately."
                : "Certificate expires within 7 days; renew or remove it if no longer needed.";
        }

        return inUse
            ? "Certificate is in use and approaching expiration; plan renewal before it expires."
            : "Certificate is approaching expiration; renew or remove it if no longer needed.";
    }
}
