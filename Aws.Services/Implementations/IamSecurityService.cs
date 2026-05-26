using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;
using Aws.Services.Interfaces;

namespace Aws.Services.Implementations;

public class IamSecurityService : IIamSecurityService
{
    private readonly IIamRepository _iamRepository;
    private readonly ILogger<IamSecurityService> _logger;

    public IamSecurityService(IIamRepository iamRepository, ILogger<IamSecurityService> logger)
    {
        _iamRepository = iamRepository;
        _logger = logger;
    }

    public async Task<IamAccessKeysResponseDto> GetAccessKeysAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scanning IAM access keys");
        IamAccessKeysResponseDto result = await _iamRepository.GetAccessKeysAsync(cancellationToken);

        _logger.LogInformation(
            "Scan complete: {RotationCount}/{TotalCount} access keys need rotation",
            result.AccessKeysNeedingRotation,
            result.TotalAccessKeys);

        return result;
    }
}