using Microsoft.AspNetCore.Mvc;
using Aws.Services.Interfaces;
using Aws.Models.Dto;

namespace Aws.Api.Controllers;

[ApiController]
[Route("api/v1/inspector")]
public class InspectorSecurityController : ControllerBase
{
    private readonly IInspectorSecurityService _inspectorSecurityService;
    private readonly ILogger<InspectorSecurityController> _logger;

    public InspectorSecurityController(
        IInspectorSecurityService inspectorSecurityService,
        ILogger<InspectorSecurityController> logger)
    {
        _inspectorSecurityService = inspectorSecurityService;
        _logger = logger;
    }

    [HttpGet("vulnerabilities")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetVulnerabilities(
        [FromQuery] string region,
        [FromQuery] string? resourceType = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            return BadRequest(new { error = "Query parameter 'region' is required (e.g. us-east-1)." });

        _logger.LogInformation(
            "GetVulnerabilities called for region {Region}, resourceType={ResourceType}, severity={Severity}, status={Status}",
            region,
            resourceType ?? "all",
            severity ?? "all",
            status ?? "all");

        InspectorVulnerabilitiesResponseDto result = await _inspectorSecurityService.GetVulnerabilitiesAsync(
            region,
            resourceType,
            severity,
            status,
            cancellationToken);

        return Ok(result);
    }
}
