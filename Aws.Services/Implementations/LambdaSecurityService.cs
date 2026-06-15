using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;
using Aws.Services.Interfaces;

namespace Aws.Services.Implementations;

public class LambdaSecurityService : ILambdaSecurityService
{
    private readonly ILambdaRepository _lambdaRepository;
    private readonly ILogger<LambdaSecurityService> _logger;

    public LambdaSecurityService(ILambdaRepository lambdaRepository, ILogger<LambdaSecurityService> logger)
    {
        _lambdaRepository = lambdaRepository;
        _logger = logger;
    }

    public async Task<LambdaPublicFunctionsResponseDto> GetPublicFunctionsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required.", nameof(region));

        _logger.LogInformation("Scanning public Lambda functions in region {Region}", region);
        LambdaPublicFunctionsResponseDto result =
            await _lambdaRepository.GetPublicFunctionsByRegionAsync(region, cancellationToken);

        _logger.LogInformation(
            "Scan complete for region {Region}: {PublicCount}/{TotalCount} Lambda functions are publicly accessible",
            result.Region,
            result.PublicFunctionsCount,
            result.TotalFunctions);

        return result;
    }
}
