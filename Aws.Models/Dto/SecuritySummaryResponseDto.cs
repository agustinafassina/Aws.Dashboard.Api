namespace Aws.Models.Dto;

public class SecuritySummaryResponseDto
{
    public required string Region { get; init; }
    public int CertificateExpirationWindowDays { get; init; }
    public int TotalFindings { get; init; }
    public int PublicBuckets { get; init; }
    public int UnencryptedBuckets { get; init; }
    public int InstancesWithOpenPorts { get; init; }
    public int UnusedSecurityGroups { get; init; }
    public int UnattachedVolumes { get; init; }
    public int RdsInstancesWithOpenPorts { get; init; }
    public int UnencryptedRdsInstances { get; init; }
    public int Imdsv1Instances { get; init; }
    public int PublicLambdaFunctions { get; init; }
    public int PublicLoadBalancers { get; init; }
    public int EcrRepositoriesAtRisk { get; init; }
    public int ExpiringCertificates { get; init; }
    public int ExpiredCertificates { get; init; }
    public int ActiveInspectorFindings { get; init; }
    public int UntaggedResources { get; init; }
    public int AccessKeysNeedingRotation { get; init; }
    public int AccessKeysNeverUsed { get; init; }
    public int UsersWithoutMfa { get; init; }
    public int RiskyPolicies { get; init; }
    public int AdminPrivilegeGrants { get; init; }
    public int CrossAccountRoles { get; init; }
    public int RootAccountRiskCount { get; init; }
    public int RiskyInlinePolicies { get; init; }
    public DateTime ScannedAt { get; init; }
}
