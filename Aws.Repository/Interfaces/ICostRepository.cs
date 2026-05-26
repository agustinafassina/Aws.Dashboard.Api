using Aws.Models.Dto;

namespace Aws.Repository.Interfaces;

public interface ICostRepository
{
    Task<CostByProjectResponseDto> GetCostsByProjectAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}