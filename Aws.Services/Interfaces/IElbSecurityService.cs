using Aws.Models.Dto;

namespace Aws.Services.Interfaces;

public interface IElbSecurityService
{
    Task<ElbPublicListenersResponseDto> GetPublicListenersByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);
}
