using Microsoft.AspNetCore.Mvc;
using Aws.Services.Interfaces;

namespace Aws.Api.Controllers;

[ApiController]
[Route("api/v1/lambda")]
public class LambdaSecurityController : ControllerBase
{
    private readonly ILambdaSecurityService _lambdaSecurityService;
    private readonly ILogger<LambdaSecurityController> _logger;

    public LambdaSecurityController(
        ILambdaSecurityService lambdaSecurityService,
        ILogger<LambdaSecurityController> logger)
    {
        _lambdaSecurityService = lambdaSecurityService;
        _logger = logger;
    }

    [HttpGet("public-functions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPublicFunctions(
        [FromQuery] string region,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            return BadRequest(new { error = "Query parameter 'region' is required (e.g. us-east-1)." });

        _logger.LogInformation("GetPublicFunctions called for region {Region}", region);

        Models.Dto.LambdaPublicFunctionsResponseDto result =
            await _lambdaSecurityService.GetPublicFunctionsByRegionAsync(region, cancellationToken);
        return Ok(result);
    }
}
