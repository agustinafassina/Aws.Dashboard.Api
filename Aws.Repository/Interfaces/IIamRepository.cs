using Aws.Models.Dto;

namespace Aws.Repository.Interfaces;

public interface IIamRepository
{
    Task<IamAccessKeysResponseDto> GetAccessKeysAsync(CancellationToken cancellationToken = default);
    Task<IamUsersWithoutMfaResponseDto> GetUsersWithoutMfaAsync(CancellationToken cancellationToken = default);
    Task<IamRiskyPoliciesResponseDto> GetRiskyPoliciesAsync(CancellationToken cancellationToken = default);

    Task<IamAdminPrivilegeGrantsResponseDto> GetAdminPrivilegeGrantsAsync(
        CancellationToken cancellationToken = default);

    Task<IamCrossAccountRolesResponseDto> GetCrossAccountRolesAsync(
        CancellationToken cancellationToken = default);

    Task<IamRootAccountStatusResponseDto> GetRootAccountStatusAsync(
        CancellationToken cancellationToken = default);

    Task<IamInlinePoliciesResponseDto> GetInlinePoliciesAsync(
        CancellationToken cancellationToken = default);

    Task<IamOverprivilegedPoliciesResponseDto> GetOverprivilegedCustomPoliciesAsync(
        CancellationToken cancellationToken = default);
}