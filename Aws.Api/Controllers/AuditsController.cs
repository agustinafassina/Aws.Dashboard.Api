using Microsoft.AspNetCore.Mvc;
using Aws.Services.Interfaces;
using Aws.Models.Dto;

namespace Aws.Api.Controllers;

[ApiController]
[Route("api/v1/audits")]
public class AuditsController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditsController> _logger;

    public AuditsController(IAuditService auditService, ILogger<AuditsController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet("untagged-resources")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUntaggedResources(
        [FromQuery] string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            return BadRequest(new { error = "Query parameter 'region' is required (e.g. us-east-1)." });

        _logger.LogInformation("GetUntaggedResources called for region {Region}", region);

        UntaggedResourcesResponseDto? result = await _auditService.GetUntaggedResourcesByRegionAsync(region, cancellationToken);
        return Ok(result);
    }

    [HttpGet("resources-by-project")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetResourcesByProjectTag(
        [FromQuery] string region,
        [FromQuery] string projectTagValue,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            return BadRequest(new { error = "Query parameter 'region' is required (e.g. us-east-1)." });

        if (string.IsNullOrWhiteSpace(projectTagValue))
            return BadRequest(new { error = "Query parameter 'projectTagValue' is required." });

        _logger.LogInformation(
            "GetResourcesByProjectTag called for region {Region}, project {ProjectTagValue}",
            region,
            projectTagValue);

        ResourcesByProjectTagResponseDto result = await _auditService.GetResourcesByProjectTagAsync(
            region,
            projectTagValue,
            cancellationToken);

        return Ok(result);
    }
}
