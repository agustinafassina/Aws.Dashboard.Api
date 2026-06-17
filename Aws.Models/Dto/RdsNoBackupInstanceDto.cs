namespace Aws.Models.Dto;

public class RdsNoBackupInstanceDto
{
    public required string DbInstanceIdentifier { get; init; }
    public required string Engine { get; init; }
    public string? EngineVersion { get; init; }
    public string? Status { get; init; }
    public int BackupRetentionPeriodDays { get; init; }
    public string? PreferredBackupWindow { get; init; }
    public DateTime? LatestRestorableTime { get; init; }
    public required string RiskReason { get; init; }
    public required string Recommendation { get; init; }
}
