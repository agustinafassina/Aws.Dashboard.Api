using Microsoft.AspNetCore.Mvc;
using Aws.Services.Interfaces;

namespace Aws.Api.Controllers;

[ApiController]
[Route("api/v1/ecr")]
public class EcrSecurityController : ControllerBase
{
    private readonly IEcrSecurityService _ecrSecurityService;
    private readonly ILogger<EcrSecurityController> _logger;

    public EcrSecurityController(IEcrSecurityService ecrSecurityService, ILogger<EcrSecurityController> logger)
    {
        _ecrSecurityService = ecrSecurityService;
        _logger = logger;
    }

    [HttpGet("repository-risks")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRepositoryRisks(
        [FromQuery] string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            return BadRequest(new { error = "Query parameter 'region' is required (e.g. us-east-1)." });

        _logger.LogInformation("GetRepositoryRisks called for region {Region}", region);

        Models.Dto.EcrRepositoryRisksResponseDto result =
            await _ecrSecurityService.GetRepositoryRisksByRegionAsync(region, cancellationToken);
        return Ok(result);
    }
}
