using Microsoft.AspNetCore.Mvc;
using Aws.Services.Interfaces;

namespace Aws.Api.Controllers;

[ApiController]
[Route("api/v1/iam")]
public class IamSecurityController : ControllerBase
{
    private readonly IIamSecurityService _iamSecurityService;
    private readonly ILogger<IamSecurityController> _logger;

    public IamSecurityController(IIamSecurityService iamSecurityService, ILogger<IamSecurityController> logger)
    {
        _iamSecurityService = iamSecurityService;
        _logger = logger;
    }

    [HttpGet("access-keys")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccessKeys(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetAccessKeys called");

        Models.Dto.IamAccessKeysResponseDto? result = await _iamSecurityService.GetAccessKeysAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("users-without-mfa")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsersWithoutMfa(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetUsersWithoutMfa called");

        Models.Dto.IamUsersWithoutMfaResponseDto result =
            await _iamSecurityService.GetUsersWithoutMfaAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("risky-policies")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRiskyPolicies(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetRiskyPolicies called");

        Models.Dto.IamRiskyPoliciesResponseDto result =
            await _iamSecurityService.GetRiskyPoliciesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("admin-privilege-grants")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdminPrivilegeGrants(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetAdminPrivilegeGrants called");

        Models.Dto.IamAdminPrivilegeGrantsResponseDto result =
            await _iamSecurityService.GetAdminPrivilegeGrantsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("cross-account-roles")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCrossAccountRoles(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetCrossAccountRoles called");

        Models.Dto.IamCrossAccountRolesResponseDto result =
            await _iamSecurityService.GetCrossAccountRolesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("root-account-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRootAccountStatus(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetRootAccountStatus called");

        Models.Dto.IamRootAccountStatusResponseDto result =
            await _iamSecurityService.GetRootAccountStatusAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("inline-policies")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInlinePolicies(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetInlinePolicies called");

        Models.Dto.IamInlinePoliciesResponseDto result =
            await _iamSecurityService.GetInlinePoliciesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("overprivileged-custom-policies")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverprivilegedCustomPolicies(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetOverprivilegedCustomPolicies called");

        Models.Dto.IamOverprivilegedPoliciesResponseDto result =
            await _iamSecurityService.GetOverprivilegedCustomPoliciesAsync(cancellationToken);
        return Ok(result);
    }
}
