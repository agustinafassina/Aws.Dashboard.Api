using Microsoft.AspNetCore.Mvc;
using Aws.Services.Interfaces;
using Aws.Models.Dto;

namespace Aws.Api.Controllers;

[ApiController]
[Route("api/v1/cost")]
public class CostController : ControllerBase
{
    private readonly ICostService _costService;
    private readonly ILogger<CostController> _logger;

    public CostController(ICostService costService, ILogger<CostController> logger)
    {
        _costService = costService;
        _logger = logger;
    }

    [HttpGet("by-project")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCostsByProject(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetCostsByProject called with startDate={StartDate}, endDate={EndDate}",
            startDate,
            endDate);

        Models.Dto.CostByProjectResponseDto? result =
            await _costService.GetCostsByProjectAsync(startDate, endDate, cancellationToken);

        return Ok(result);
    }
}
