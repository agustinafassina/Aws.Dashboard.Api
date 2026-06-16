using Microsoft.AspNetCore.Mvc;
using Aws.Services.Interfaces;

namespace Aws.Api.Controllers;

[ApiController]
[Route("api/v1/elb")]
public class ElbSecurityController : ControllerBase
{
    private readonly IElbSecurityService _elbSecurityService;
    private readonly ILogger<ElbSecurityController> _logger;

    public ElbSecurityController(IElbSecurityService elbSecurityService, ILogger<ElbSecurityController> logger)
    {
        _elbSecurityService = elbSecurityService;
        _logger = logger;
    }

    [HttpGet("public-listeners")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPublicListeners(
        [FromQuery] string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            return BadRequest(new { error = "Query parameter 'region' is required (e.g. us-east-1)." });

        _logger.LogInformation("GetPublicListeners called for region {Region}", region);

        Models.Dto.ElbPublicListenersResponseDto result =
            await _elbSecurityService.GetPublicListenersByRegionAsync(region, cancellationToken);
        return Ok(result);
    }
}
