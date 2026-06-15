namespace Aws.Models.Dto;

public class LambdaPublicFunctionsResponseDto
{
    public required string Region { get; init; }
    public int TotalFunctions { get; init; }
    public int PublicFunctionsCount { get; init; }
    public required IReadOnlyList<LambdaPublicFunctionDto> Functions { get; init; }
    public DateTime ScannedAt { get; init; }
}
