using Aws.Models.Dto;

namespace Aws.Repository.Interfaces;

public interface IElbRepository
{
    Task<ElbPublicListenersResponseDto> GetPublicListenersByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);
}
