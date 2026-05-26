using Aws.Models.Dto;

namespace Aws.Services.Interfaces;

public interface ICostService
{
    Task<CostByProjectResponseDto> GetCostsByProjectAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}
