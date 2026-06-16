namespace Aws.Models.Dto;

public class EcrRepositoryRisksResponseDto
{
    public required string Region { get; init; }
    public int TotalRepositoriesScanned { get; init; }
    public int RepositoriesAtRiskCount { get; init; }
    public required IReadOnlyList<EcrRepositoryRiskDto> Repositories { get; init; }
    public DateTime ScannedAt { get; init; }
}
