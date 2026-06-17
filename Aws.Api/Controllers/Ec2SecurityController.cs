using Microsoft.AspNetCore.Mvc;
using Aws.Services.Interfaces;

namespace Aws.Api.Controllers;

[ApiController]
[Route("api/v1/ec2")]
public class Ec2SecurityController : ControllerBase
{
    private readonly IEc2SecurityService _ec2SecurityService;
    private readonly ILogger<Ec2SecurityController> _logger;

    public Ec2SecurityController(IEc2SecurityService ec2SecurityService, ILogger<Ec2SecurityController> logger)
    {
        _ec2SecurityService = ec2SecurityService;
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

        var result = await _ec2SecurityService.GetInstancesWithOpenPortsByRegionAsync(region, cancellationToken);
        return Ok(result);
    }

    [HttpGet("unused-security-groups")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUnusedSecurityGroups(
        [FromQuery] string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            return BadRequest(new { error = "Query parameter 'region' is required (e.g. us-east-1)." });

        _logger.LogInformation("GetUnusedSecurityGroups called for region {Region}", region);

        Models.Dto.Ec2UnusedSecurityGroupsResponseDto result =
            await _ec2SecurityService.GetUnusedSecurityGroupsByRegionAsync(region, cancellationToken);
        return Ok(result);
    }

    [HttpGet("unattached-volumes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUnattachedVolumes(
        [FromQuery] string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            return BadRequest(new { error = "Query parameter 'region' is required (e.g. us-east-1)." });

        _logger.LogInformation("GetUnattachedVolumes called for region {Region}", region);

        Models.Dto.Ec2UnattachedVolumesResponseDto result =
            await _ec2SecurityService.GetUnattachedVolumesByRegionAsync(region, cancellationToken);
        return Ok(result);
    }

    [HttpGet("imdsv1-instances")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetImdsv1Instances(
        [FromQuery] string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            return BadRequest(new { error = "Query parameter 'region' is required (e.g. us-east-1)." });

        _logger.LogInformation("GetImdsv1Instances called for region {Region}", region);

        Models.Dto.Ec2Imdsv1InstancesResponseDto result =
            await _ec2SecurityService.GetImdsv1InstancesByRegionAsync(region, cancellationToken);
        return Ok(result);
    }
}
