using Microsoft.AspNetCore.Mvc;
using Aws.Services.Interfaces;

namespace Aws.Api.Controllers;

[ApiController]
[Route("api/v1/security")]
public class SecuritySummaryController : ControllerBase
{
    private readonly ISecuritySummaryService _securitySummaryService;
    private readonly ILogger<SecuritySummaryController> _logger;

    public SecuritySummaryController(
        ISecuritySummaryService securitySummaryService,
        ILogger<SecuritySummaryController> logger)
    {
        _securitySummaryService = securitySummaryService;
        _logger = logger;
    }

    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] string region,
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            return BadRequest(new { error = "Query parameter 'region' is required (e.g. us-east-1)." });

        _logger.LogInformation("GetSummary called for region {Region}", region);

        try
        {
            Models.Dto.SecuritySummaryResponseDto result =
                await _securitySummaryService.GetSummaryByRegionAsync(region, days, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
