namespace Aws.Models.Dto;

public class IamRootAccountStatusResponseDto
{
    public required string AccountId { get; init; }
    public bool MfaEnabled { get; init; }
    public bool HasActiveAccessKeys { get; init; }
    public bool HasSigningCertificate { get; init; }
    public bool PasswordEnabled { get; init; }
    public bool AccessKey1Active { get; init; }
    public bool AccessKey2Active { get; init; }
    public DateTime? PasswordLastUsed { get; init; }
    public DateTime? AccessKey1LastUsed { get; init; }
    public DateTime? AccessKey2LastUsed { get; init; }
    public bool CredentialReportAvailable { get; init; }
    public bool IsCompliant { get; init; }
    public required IReadOnlyList<string> RiskReasons { get; init; }
    public required string Recommendation { get; init; }
    public DateTime ScannedAt { get; init; }
}
