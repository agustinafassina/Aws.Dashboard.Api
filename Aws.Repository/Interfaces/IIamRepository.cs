using Aws.Models.Dto;

namespace Aws.Repository.Interfaces;

public interface IIamRepository
{
    Task<IamAccessKeysResponseDto> GetAccessKeysAsync(CancellationToken cancellationToken = default);
    Task<IamUsersWithoutMfaResponseDto> GetUsersWithoutMfaAsync(CancellationToken cancellationToken = default);
    Task<IamRiskyPoliciesResponseDto> GetRiskyPoliciesAsync(CancellationToken cancellationToken = default);
}