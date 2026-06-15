namespace Aws.Models.Dto;

public class AcmExpiringCertificatesResponseDto
{
    public required string Region { get; init; }
    public int ExpirationWindowDays { get; init; }
    public int TotalCertificatesScanned { get; init; }
    public int ExpiringCertificatesCount { get; init; }
    public int ExpiredCertificatesCount { get; init; }
    public required IReadOnlyList<AcmExpiringCertificateDto> Certificates { get; init; }
    public DateTime ScannedAt { get; init; }
}
