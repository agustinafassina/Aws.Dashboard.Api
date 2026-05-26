using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;
using Aws.Services.Interfaces;

namespace Aws.Services.Implementations;

public class CostService : ICostService
{
    private readonly ICostRepository _costRepository;
    private readonly ILogger<CostService> _logger;

    public CostService(ICostRepository costRepository, ILogger<CostService> logger)
    {
        _costRepository = costRepository;
        _logger = logger;
    }

    public async Task<CostByProjectResponseDto> GetCostsByProjectAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate == default)
            throw new ArgumentException("startDate is required (yyyy-MM-dd).", nameof(startDate));

        if (endDate == default)
            throw new ArgumentException("endDate is required (yyyy-MM-dd).", nameof(endDate));

        if (startDate > endDate)
            throw new ArgumentException("startDate must be on or before endDate.", nameof(startDate));

        _logger.LogInformation(
            "Fetching costs by project from {StartDate} to {EndDate}",
            startDate,
            endDate);

        CostByProjectResponseDto result =
            await _costRepository.GetCostsByProjectAsync(startDate, endDate, cancellationToken);

        _logger.LogInformation(
            "Cost query complete: {ProjectCount} projects, total {TotalAmount} {Currency}",
            result.Projects.Count,
            result.TotalAmount,
            result.Currency);

        return result;
    }
}