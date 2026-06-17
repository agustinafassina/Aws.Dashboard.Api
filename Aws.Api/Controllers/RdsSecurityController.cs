using Microsoft.AspNetCore.Mvc;
using Aws.Services.Interfaces;

namespace Aws.Api.Controllers;

[ApiController]
[Route("api/v1/rds")]
public class RdsSecurityController : ControllerBase
{
    private readonly IRdsSecurityService _rdsSecurityService;
    private readonly ILogger<RdsSecurityController> _logger;

    public RdsSecurityController(IRdsSecurityService rdsSecurityService, ILogger<RdsSecurityController> logger)
    {
        _rdsSecurityService = rdsSecurityService;
        _logger = logger;
    }

    [HttpGet("open-ports")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetOpenPorts(
        [FromQuery] string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            return BadRequest(new { error = "Query parameter 'region' is required (e.g. us-east-1)." });

        _logger.LogInformation("GetOpenPorts called for region {Region}", region);

        var result = await _rdsSecurityService.GetInstancesWithOpenPortsByRegionAsync(region, cancellationToken);
        return Ok(result);
    }

    [HttpGet("unencrypted-instances")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUnencryptedInstances(
        [FromQuery] string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            return BadRequest(new { error = "Query parameter 'region' is required (e.g. us-east-1)." });

        _logger.LogInformation("GetUnencryptedInstances called for region {Region}", region);

        Models.Dto.RdsUnencryptedInstancesResponseDto result =
            await _rdsSecurityService.GetUnencryptedInstancesByRegionAsync(region, cancellationToken);
        return Ok(result);
    }
}
