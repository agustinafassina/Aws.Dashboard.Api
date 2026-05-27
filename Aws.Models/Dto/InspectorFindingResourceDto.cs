namespace Aws.Models.Dto;

public class InspectorFindingResourceDto
{
    public string? InstanceId { get; init; }
    public string? VpcId { get; init; }
    public string? SubnetId { get; init; }
    public string? ImageId { get; init; }
    public string? FunctionName { get; init; }
    public string? Runtime { get; init; }
    public string? RepositoryName { get; init; }
    public string? ImageHash { get; init; }
    public IReadOnlyList<string>? ImageTags { get; init; }
}
