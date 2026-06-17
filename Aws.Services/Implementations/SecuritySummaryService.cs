using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Services.Interfaces;

namespace Aws.Services.Implementations;

public class SecuritySummaryService : ISecuritySummaryService
{
    private const int MinCertificateWindowDays = 1;
    private const int MaxCertificateWindowDays = 365;

    private readonly IS3SecurityService _s3SecurityService;
    private readonly IEc2SecurityService _ec2SecurityService;
    private readonly IRdsSecurityService _rdsSecurityService;
    private readonly ILambdaSecurityService _lambdaSecurityService;
    private readonly IElbSecurityService _elbSecurityService;
    private readonly IEcrSecurityService _ecrSecurityService;
    private readonly IAcmSecurityService _acmSecurityService;
    private readonly IInspectorSecurityService _inspectorSecurityService;
    private readonly IAuditService _auditService;
    private readonly IIamSecurityService _iamSecurityService;
    private readonly ILogger<SecuritySummaryService> _logger;

    public SecuritySummaryService(
        IS3SecurityService s3SecurityService,
        IEc2SecurityService ec2SecurityService,
        IRdsSecurityService rdsSecurityService,
        ILambdaSecurityService lambdaSecurityService,
        IElbSecurityService elbSecurityService,
        IEcrSecurityService ecrSecurityService,
        IAcmSecurityService acmSecurityService,
        IInspectorSecurityService inspectorSecurityService,
        IAuditService auditService,
        IIamSecurityService iamSecurityService,
        ILogger<SecuritySummaryService> logger)
    {
        _s3SecurityService = s3SecurityService;
        _ec2SecurityService = ec2SecurityService;
        _rdsSecurityService = rdsSecurityService;
        _lambdaSecurityService = lambdaSecurityService;
        _elbSecurityService = elbSecurityService;
        _ecrSecurityService = ecrSecurityService;
        _acmSecurityService = acmSecurityService;
        _inspectorSecurityService = inspectorSecurityService;
        _auditService = auditService;
        _iamSecurityService = iamSecurityService;
        _logger = logger;
    }

    public async Task<SecuritySummaryResponseDto> GetSummaryByRegionAsync(
        string region,
        int certificateExpirationWindowDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region is required.", nameof(region));

        if (certificateExpirationWindowDays < MinCertificateWindowDays ||
            certificateExpirationWindowDays > MaxCertificateWindowDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(certificateExpirationWindowDays),
                certificateExpirationWindowDays,
                $"Days must be between {MinCertificateWindowDays} and {MaxCertificateWindowDays}.");
        }

        _logger.LogInformation("Building security summary for region {Region}", region);

        Task<S3PublicBucketsResponseDto> publicBucketsTask =
            _s3SecurityService.GetPublicBucketsByRegionAsync(region, cancellationToken);
        Task<S3EncryptionStatusResponseDto> encryptionTask =
            _s3SecurityService.GetEncryptionStatusByRegionAsync(region, cancellationToken);
        Task<Ec2OpenPortsResponseDto> ec2OpenPortsTask =
            _ec2SecurityService.GetInstancesWithOpenPortsByRegionAsync(region, cancellationToken);
        Task<Ec2UnusedSecurityGroupsResponseDto> unusedSecurityGroupsTask =
            _ec2SecurityService.GetUnusedSecurityGroupsByRegionAsync(region, cancellationToken);
        Task<Ec2UnattachedVolumesResponseDto> unattachedVolumesTask =
            _ec2SecurityService.GetUnattachedVolumesByRegionAsync(region, cancellationToken);
        Task<Ec2Imdsv1InstancesResponseDto> imdsv1InstancesTask =
            _ec2SecurityService.GetImdsv1InstancesByRegionAsync(region, cancellationToken);
        Task<RdsOpenPortsResponseDto> rdsOpenPortsTask =
            _rdsSecurityService.GetInstancesWithOpenPortsByRegionAsync(region, cancellationToken);
        Task<RdsUnencryptedInstancesResponseDto> unencryptedRdsTask =
            _rdsSecurityService.GetUnencryptedInstancesByRegionAsync(region, cancellationToken);
        Task<LambdaPublicFunctionsResponseDto> lambdaTask =
            _lambdaSecurityService.GetPublicFunctionsByRegionAsync(region, cancellationToken);
        Task<ElbPublicListenersResponseDto> elbTask =
            _elbSecurityService.GetPublicListenersByRegionAsync(region, cancellationToken);
        Task<EcrRepositoryRisksResponseDto> ecrTask =
            _ecrSecurityService.GetRepositoryRisksByRegionAsync(region, cancellationToken);
        Task<AcmExpiringCertificatesResponseDto> acmTask =
            _acmSecurityService.GetExpiringCertificatesByRegionAsync(
                region,
                certificateExpirationWindowDays,
                cancellationToken);
        Task<InspectorVulnerabilitiesResponseDto> inspectorTask =
            _inspectorSecurityService.GetVulnerabilitiesAsync(
                region,
                resourceType: null,
                severity: null,
                status: "ACTIVE",
                cancellationToken);
        Task<UntaggedResourcesResponseDto> untaggedTask =
            _auditService.GetUntaggedResourcesByRegionAsync(region, cancellationToken);

        Task<IamAccessKeysResponseDto> accessKeysTask =
            _iamSecurityService.GetAccessKeysAsync(cancellationToken);
        Task<IamUsersWithoutMfaResponseDto> usersWithoutMfaTask =
            _iamSecurityService.GetUsersWithoutMfaAsync(cancellationToken);
        Task<IamRiskyPoliciesResponseDto> riskyPoliciesTask =
            _iamSecurityService.GetRiskyPoliciesAsync(cancellationToken);
        Task<IamAdminPrivilegeGrantsResponseDto> adminGrantsTask =
            _iamSecurityService.GetAdminPrivilegeGrantsAsync(cancellationToken);
        Task<IamCrossAccountRolesResponseDto> crossAccountRolesTask =
            _iamSecurityService.GetCrossAccountRolesAsync(cancellationToken);
        Task<IamRootAccountStatusResponseDto> rootAccountTask =
            _iamSecurityService.GetRootAccountStatusAsync(cancellationToken);
        Task<IamInlinePoliciesResponseDto> inlinePoliciesTask =
            _iamSecurityService.GetInlinePoliciesAsync(cancellationToken);

        await Task.WhenAll(
            publicBucketsTask,
            encryptionTask,
            ec2OpenPortsTask,
            unusedSecurityGroupsTask,
            unattachedVolumesTask,
            imdsv1InstancesTask,
            rdsOpenPortsTask,
            unencryptedRdsTask,
            lambdaTask,
            elbTask,
            ecrTask,
            acmTask,
            inspectorTask,
            untaggedTask,
            accessKeysTask,
            usersWithoutMfaTask,
            riskyPoliciesTask,
            adminGrantsTask,
            crossAccountRolesTask,
            rootAccountTask,
            inlinePoliciesTask);

        S3PublicBucketsResponseDto publicBuckets = await publicBucketsTask;
        S3EncryptionStatusResponseDto encryption = await encryptionTask;
        Ec2OpenPortsResponseDto ec2OpenPorts = await ec2OpenPortsTask;
        Ec2UnusedSecurityGroupsResponseDto unusedSecurityGroups = await unusedSecurityGroupsTask;
        Ec2UnattachedVolumesResponseDto unattachedVolumes = await unattachedVolumesTask;
        Ec2Imdsv1InstancesResponseDto imdsv1Instances = await imdsv1InstancesTask;
        RdsOpenPortsResponseDto rdsOpenPorts = await rdsOpenPortsTask;
        RdsUnencryptedInstancesResponseDto unencryptedRds = await unencryptedRdsTask;
        LambdaPublicFunctionsResponseDto lambda = await lambdaTask;
        ElbPublicListenersResponseDto elb = await elbTask;
        EcrRepositoryRisksResponseDto ecr = await ecrTask;
        AcmExpiringCertificatesResponseDto acm = await acmTask;
        InspectorVulnerabilitiesResponseDto inspector = await inspectorTask;
        UntaggedResourcesResponseDto untagged = await untaggedTask;
        IamAccessKeysResponseDto accessKeys = await accessKeysTask;
        IamUsersWithoutMfaResponseDto usersWithoutMfa = await usersWithoutMfaTask;
        IamRiskyPoliciesResponseDto riskyPolicies = await riskyPoliciesTask;
        IamAdminPrivilegeGrantsResponseDto adminGrants = await adminGrantsTask;
        IamCrossAccountRolesResponseDto crossAccountRoles = await crossAccountRolesTask;
        IamRootAccountStatusResponseDto rootAccount = await rootAccountTask;
        IamInlinePoliciesResponseDto inlinePolicies = await inlinePoliciesTask;

        int totalFindings =
            publicBuckets.PublicBucketsCount +
            encryption.UnencryptedBucketsCount +
            ec2OpenPorts.InstancesWithPublicPorts +
            unusedSecurityGroups.UnusedSecurityGroupsCount +
            unattachedVolumes.UnattachedVolumesCount +
            rdsOpenPorts.InstancesWithPublicPorts +
            unencryptedRds.UnencryptedInstancesCount +
            imdsv1Instances.Imdsv1InstancesCount +
            lambda.PublicFunctionsCount +
            elb.PublicLoadBalancersCount +
            ecr.RepositoriesAtRiskCount +
            acm.ExpiringCertificatesCount +
            acm.ExpiredCertificatesCount +
            inspector.TotalFindings +
            untagged.UntaggedResourcesCount +
            accessKeys.AccessKeysNeedingRotation +
            accessKeys.AccessKeysNeverUsed +
            usersWithoutMfa.UsersWithoutMfa +
            riskyPolicies.RiskyPolicyCount +
            adminGrants.AdminPrivilegeGrantCount +
            crossAccountRoles.CrossAccountRolesCount +
            rootAccount.RiskReasons.Count +
            inlinePolicies.RiskyInlinePolicyCount;

        SecuritySummaryResponseDto summary = new()
        {
            Region = publicBuckets.Region,
            CertificateExpirationWindowDays = certificateExpirationWindowDays,
            TotalFindings = totalFindings,
            PublicBuckets = publicBuckets.PublicBucketsCount,
            UnencryptedBuckets = encryption.UnencryptedBucketsCount,
            InstancesWithOpenPorts = ec2OpenPorts.InstancesWithPublicPorts,
            UnusedSecurityGroups = unusedSecurityGroups.UnusedSecurityGroupsCount,
            UnattachedVolumes = unattachedVolumes.UnattachedVolumesCount,
            RdsInstancesWithOpenPorts = rdsOpenPorts.InstancesWithPublicPorts,
            UnencryptedRdsInstances = unencryptedRds.UnencryptedInstancesCount,
            Imdsv1Instances = imdsv1Instances.Imdsv1InstancesCount,
            PublicLambdaFunctions = lambda.PublicFunctionsCount,
            PublicLoadBalancers = elb.PublicLoadBalancersCount,
            EcrRepositoriesAtRisk = ecr.RepositoriesAtRiskCount,
            ExpiringCertificates = acm.ExpiringCertificatesCount,
            ExpiredCertificates = acm.ExpiredCertificatesCount,
            ActiveInspectorFindings = inspector.TotalFindings,
            UntaggedResources = untagged.UntaggedResourcesCount,
            AccessKeysNeedingRotation = accessKeys.AccessKeysNeedingRotation,
            AccessKeysNeverUsed = accessKeys.AccessKeysNeverUsed,
            UsersWithoutMfa = usersWithoutMfa.UsersWithoutMfa,
            RiskyPolicies = riskyPolicies.RiskyPolicyCount,
            AdminPrivilegeGrants = adminGrants.AdminPrivilegeGrantCount,
            CrossAccountRoles = crossAccountRoles.CrossAccountRolesCount,
            RootAccountRiskCount = rootAccount.RiskReasons.Count,
            RiskyInlinePolicies = inlinePolicies.RiskyInlinePolicyCount,
            ScannedAt = DateTime.UtcNow
        };

        _logger.LogInformation(
            "Security summary complete for region {Region}: {TotalFindings} total findings",
            summary.Region,
            summary.TotalFindings);

        return summary;
    }
}
