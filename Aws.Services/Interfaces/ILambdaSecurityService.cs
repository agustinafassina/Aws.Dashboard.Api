using Aws.Models.Dto;

namespace Aws.Services.Interfaces;

public interface ILambdaSecurityService
{
    Task<LambdaPublicFunctionsResponseDto> GetPublicFunctionsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default);
}
