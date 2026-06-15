namespace Aws.Models.Dto;

public class AcmExpiringCertificateDto
{
    public required string CertificateArn { get; init; }
    public required string DomainName { get; init; }
    public required IReadOnlyList<string> SubjectAlternativeNames { get; init; }
    public string? Status { get; init; }
    public string? Type { get; init; }
    public bool InUse { get; init; }
    public DateTime? NotBefore { get; init; }
    public DateTime? NotAfter { get; init; }
    public int DaysUntilExpiry { get; init; }
    public bool IsExpired { get; init; }
    public required string Recommendation { get; init; }
}
