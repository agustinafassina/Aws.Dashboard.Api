namespace Aws.Models.Dto;

public class LambdaPublicFunctionDto
{
    public required string FunctionName { get; init; }
    public required string FunctionArn { get; init; }
    public string? Runtime { get; init; }
    public string? State { get; init; }
    public string? FunctionUrl { get; init; }
    public required IReadOnlyList<string> PublicAccessReasons { get; init; }
    public required string Recommendation { get; init; }
}
