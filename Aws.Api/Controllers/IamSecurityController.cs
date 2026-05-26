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
}
