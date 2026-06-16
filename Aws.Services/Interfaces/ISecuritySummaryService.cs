using Aws.Models.Dto;

namespace Aws.Services.Interfaces;

public interface ISecuritySummaryService
{
    Task<SecuritySummaryResponseDto> GetSummaryByRegionAsync(
        string region,
        int certificateExpirationWindowDays = 30,
        CancellationToken cancellationToken = default);
}
