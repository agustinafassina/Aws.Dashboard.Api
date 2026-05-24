using Microsoft.AspNetCore.Mvc;
using Aws.Services.Interfaces;

namespace Aws.Api.Controllers;

[ApiController]
[Route("api/v1/s3")]
public class S3SecurityController : ControllerBase
{
    private readonly IS3SecurityService _s3SecurityService;
    private readonly ILogger<S3SecurityController> _logger;

    public S3SecurityController(IS3SecurityService s3SecurityService, ILogger<S3SecurityController> logger)
    {
        _s3SecurityService = s3SecurityService;
        _logger = logger;
    }

    [HttpGet("public-buckets")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPublicBuckets(
        [FromQuery] string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            return BadRequest(new { error = "Query parameter 'region' is required (e.g. us-east-1)." });

        _logger.LogInformation("GetPublicBuckets called for region {Region}", region);

        var result = await _s3SecurityService.GetPublicBucketsByRegionAsync(region, cancellationToken);
        return Ok(result);
    }
}
