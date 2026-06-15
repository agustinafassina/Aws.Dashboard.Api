using Aws.Models.Dto;

namespace Aws.Repository.Interfaces;

public interface IAcmRepository
{
    Task<AcmExpiringCertificatesResponseDto> GetExpiringCertificatesByRegionAsync(
        string region,
        int expirationWindowDays,
        CancellationToken cancellationToken = default);
}
