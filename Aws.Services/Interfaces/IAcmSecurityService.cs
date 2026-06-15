using Aws.Models.Dto;

namespace Aws.Services.Interfaces;

public interface IAcmSecurityService
{
    Task<AcmExpiringCertificatesResponseDto> GetExpiringCertificatesByRegionAsync(
        string region,
        int expirationWindowDays,
        CancellationToken cancellationToken = default);
}
