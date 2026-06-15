using Microsoft.AspNetCore.Mvc;
using Aws.Services.Interfaces;

namespace Aws.Api.Controllers;

[ApiController]
[Route("api/v1/acm")]
public class AcmSecurityController : ControllerBase
{
    private readonly IAcmSecurityService _acmSecurityService;
    private readonly ILogger<AcmSecurityController> _logger;

    public AcmSecurityController(IAcmSecurityService acmSecurityService, ILogger<AcmSecurityController> logger)
    {
        _acmSecurityService = acmSecurityService;
        _logger = logger;
    }

    [HttpGet("expiring-certificates")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetExpiringCertificates(
        [FromQuery] string region,
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            return BadRequest(new { error = "Query parameter 'region' is required (e.g. us-east-1)." });

        if (days is < 1 or > 365)
            return BadRequest(new { error = "Query parameter 'days' must be between 1 and 365." });

        _logger.LogInformation(
            "GetExpiringCertificates called for region {Region}, days {Days}",
            region,
            days);

        try
        {
            Models.Dto.AcmExpiringCertificatesResponseDto result =
                await _acmSecurityService.GetExpiringCertificatesByRegionAsync(region, days, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
