using Aws.Models.Dto;

namespace Aws.Services.Interfaces;

public interface IIamSecurityService
{
    Task<IamAccessKeysResponseDto> GetAccessKeysAsync(CancellationToken cancellationToken = default);
}