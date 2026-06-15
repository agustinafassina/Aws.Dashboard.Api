using Aws.Models.Dto;

namespace Aws.Repository.Interfaces;

public interface ILambdaRepository
{
    Task<LambdaPublicFunctionsResponseDto> GetPublicFunctionsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);
}
