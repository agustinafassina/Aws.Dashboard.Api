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

    public async Task<IamUsersWithoutMfaResponseDto> GetUsersWithoutMfaAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scanning IAM users without MFA");
        IamUsersWithoutMfaResponseDto result =
            await _iamRepository.GetUsersWithoutMfaAsync(cancellationToken);

        _logger.LogInformation(
            "MFA scan complete: {WithoutMfa}/{ConsoleUsers} console users without MFA",
            result.UsersWithoutMfa,
            result.UsersWithConsoleAccess);

        return result;
    }

    public async Task<IamRiskyPoliciesResponseDto> GetRiskyPoliciesAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scanning IAM customer policies for wildcard admin");
        IamRiskyPoliciesResponseDto result =
            await _iamRepository.GetRiskyPoliciesAsync(cancellationToken);

        _logger.LogInformation(
            "Policy scan complete: {Risky}/{Total} customer policies with *:* allow",
            result.RiskyPolicyCount,
            result.TotalCustomerPoliciesScanned);

        return result;
    }

    public async Task<IamAdminPrivilegeGrantsResponseDto> GetAdminPrivilegeGrantsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scanning IAM admin privilege grants");
        IamAdminPrivilegeGrantsResponseDto result =
            await _iamRepository.GetAdminPrivilegeGrantsAsync(cancellationToken);

        _logger.LogInformation(
            "Admin privilege scan complete: {GrantCount} grants across {Users} users, {Groups} groups, {Roles} roles",
            result.AdminPrivilegeGrantCount,
            result.UsersWithAdminAccess,
            result.GroupsWithAdminAccess,
            result.RolesWithAdminAccess);

        return result;
    }

    public async Task<IamCrossAccountRolesResponseDto> GetCrossAccountRolesAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scanning IAM roles for cross-account trust policies");
        IamCrossAccountRolesResponseDto result =
            await _iamRepository.GetCrossAccountRolesAsync(cancellationToken);

        _logger.LogInformation(
            "Cross-account trust scan complete: {CrossAccountCount}/{TotalCount} roles flagged",
            result.CrossAccountRolesCount,
            result.TotalRolesScanned);

        return result;
    }
}