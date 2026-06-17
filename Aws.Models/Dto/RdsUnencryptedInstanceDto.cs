namespace Aws.Models.Dto;

public class RdsUnencryptedInstanceDto
{
    public required string DbInstanceIdentifier { get; init; }
    public required string Engine { get; init; }
    public string? EngineVersion { get; init; }
    public string? Status { get; init; }
    public string? StorageType { get; init; }
    public int? AllocatedStorageGiB { get; init; }
    public bool StorageEncrypted { get; init; }
    public string? KmsKeyId { get; init; }
    public required string Recommendation { get; init; }
}
