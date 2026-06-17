using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aws.Models.Configuration;
using Aws.Models.Dto;
using Aws.Repository.Helpers;
using Aws.Repository.Interfaces;

namespace Aws.Repository.Implementations;

public class IamRepository : IIamRepository
{
    private readonly IAmazonIdentityManagementService _iamClient;
    private readonly ILogger<IamRepository> _logger;
    private readonly int _rotationMaxAgeDays;
    private string? _accountId;

    public IamRepository(
        IAmazonIdentityManagementService iamClient,
        IOptions<IamOptions> iamOptions,
        ILogger<IamRepository> logger)
    {
        _iamClient = iamClient;
        _logger = logger;
        _rotationMaxAgeDays = iamOptions.Value.AccessKeyRotationMaxAgeDays;
    }

    public async Task<IamAccessKeysResponseDto> GetAccessKeysAsync(CancellationToken cancellationToken = default)
    {
        List<User> users = await ListUsersAsync(cancellationToken);
        List<IamAccessKeyDto> accessKeys = new();

        foreach (var user in users)
        {
            if (string.IsNullOrWhiteSpace(user.UserName))
                continue;

            List<IamAccessKeyDto> userKeys = await ListAccessKeysForUserAsync(user.UserName, cancellationToken);
            accessKeys.AddRange(userKeys);
        }

        _logger.LogInformation(
            "Found {KeyCount} IAM access keys across {UserCount} users",
            accessKeys.Count,
            users.Count);

        return new IamAccessKeysResponseDto
        {
            TotalUsers = users.Count,
            TotalAccessKeys = accessKeys.Count,
            AccessKeysNeedingRotation = accessKeys.Count(k => k.NeedsRotation),
            AccessKeysNeverUsed = accessKeys.Count(k => k.IsNeverUsed),
            AccessKeyRotationMaxAgeDays = _rotationMaxAgeDays,
            AccessKeys = accessKeys
                .OrderBy(k => k.UserName, StringComparer.Ordinal)
                .ThenBy(k => k.AccessKeyId, StringComparer.Ordinal)
                .ToList(),
            ScannedAt = DateTime.UtcNow
        };
    }

    private async Task<List<User>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var users = new List<User>();
        string? marker = null;

        do
        {
            ListUsersResponse response = await _iamClient.ListUsersAsync(
                new ListUsersRequest { Marker = marker },
                cancellationToken);

            users.AddRange(response.Users ?? []);
            marker = response.IsTruncated == true ? response.Marker : null;
        }
        while (!string.IsNullOrEmpty(marker));

        return users;
    }

    private async Task<List<IamAccessKeyDto>> ListAccessKeysForUserAsync(
        string userName,
        CancellationToken cancellationToken)
    {
        List<IamAccessKeyDto> keys = new();
        string? marker = null;

        do
        {
            ListAccessKeysResponse response = await _iamClient.ListAccessKeysAsync(
                new ListAccessKeysRequest { UserName = userName, Marker = marker },
                cancellationToken);

            foreach (var metadata in response.AccessKeyMetadata ?? [])
            {
                if (metadata is null)
                    continue;

                keys.Add(await MapAccessKeyAsync(userName, metadata, cancellationToken));
            }

            marker = response.IsTruncated == true ? response.Marker : null;
        }
        while (!string.IsNullOrEmpty(marker));

        return keys;
    }

    private async Task<IamAccessKeyDto> MapAccessKeyAsync(
        string userName,
        AccessKeyMetadata metadata,
        CancellationToken cancellationToken)
    {
        DateTime createDate = metadata.CreateDate.GetValueOrDefault();
        int ageInDays = (int)(DateTime.UtcNow - createDate.ToUniversalTime()).TotalDays;
        string status = metadata.Status?.Value ?? "Unknown";
        bool needsRotation = status == StatusType.Active && ageInDays >= _rotationMaxAgeDays;

        DateTime? lastUsedDate = null;
        string? lastUsedService = null;
        string? lastUsedRegion = null;

        if (status == StatusType.Active && !string.IsNullOrWhiteSpace(metadata.AccessKeyId))
        {
            try
            {
                GetAccessKeyLastUsedResponse lastUsed = await _iamClient.GetAccessKeyLastUsedAsync(
                    new GetAccessKeyLastUsedRequest { AccessKeyId = metadata.AccessKeyId },
                    cancellationToken);

                lastUsedDate = lastUsed.AccessKeyLastUsed?.LastUsedDate;
                lastUsedService = lastUsed.AccessKeyLastUsed?.ServiceName;
                lastUsedRegion = lastUsed.AccessKeyLastUsed?.Region;
            }
            catch (AmazonIdentityManagementServiceException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not retrieve last used info for access key {AccessKeyId} of user {UserName}",
                    metadata.AccessKeyId,
                    userName);
            }
        }

        bool isNeverUsed = status == StatusType.Active && lastUsedDate is null;

        return new IamAccessKeyDto
        {
            UserName = userName,
            AccessKeyId = metadata.AccessKeyId ?? "unknown",
            Status = status,
            CreateDate = createDate,
            AgeInDays = ageInDays,
            LastUsedDate = lastUsedDate,
            LastUsedService = lastUsedService,
            LastUsedRegion = lastUsedRegion,
            NeedsRotation = needsRotation,
            IsNeverUsed = isNeverUsed,
            Recommendation = BuildRecommendation(status, needsRotation, ageInDays, lastUsedDate, isNeverUsed)
        };
    }

    public async Task<IamUsersWithoutMfaResponseDto> GetUsersWithoutMfaAsync(
        CancellationToken cancellationToken = default)
    {
        List<User> users = await ListUsersAsync(cancellationToken);
        List<IamUserWithoutMfaDto> usersWithoutMfa = new();
        int usersWithConsoleAccess = 0;

        foreach (User user in users)
        {
            if (string.IsNullOrWhiteSpace(user.UserName))
                continue;

            if (!await UserHasConsoleAccessAsync(user.UserName, cancellationToken))
                continue;

            usersWithConsoleAccess++;

            if (await UserHasMfaDeviceAsync(user.UserName, cancellationToken))
                continue;

            usersWithoutMfa.Add(new IamUserWithoutMfaDto
            {
                UserName = user.UserName,
                Email = await ResolveUserEmailAsync(user.UserName, cancellationToken),
                UserCreated = user.CreateDate,
                Recommendation =
                    "Console user has no MFA device. Enable MFA for all users with password sign-in.",
            });
        }

        return new IamUsersWithoutMfaResponseDto
        {
            TotalUsers = users.Count,
            UsersWithConsoleAccess = usersWithConsoleAccess,
            UsersWithoutMfa = usersWithoutMfa.Count,
            Users = usersWithoutMfa
                .OrderBy(u => u.UserName, StringComparer.Ordinal)
                .ToList(),
            ScannedAt = DateTime.UtcNow,
        };
    }

    public async Task<IamRiskyPoliciesResponseDto> GetRiskyPoliciesAsync(
        CancellationToken cancellationToken = default)
    {
        List<ManagedPolicy> policies = await ListCustomerManagedPoliciesAsync(cancellationToken);
        List<IamRiskyPolicyDto> riskyPolicies = new();

        foreach (ManagedPolicy policy in policies)
        {
            if (string.IsNullOrWhiteSpace(policy.Arn) || string.IsNullOrWhiteSpace(policy.PolicyName))
                continue;

            string? riskReason = await TryGetPolicyWildcardRiskAsync(policy.Arn, cancellationToken);
            if (riskReason is null)
                continue;

            riskyPolicies.Add(new IamRiskyPolicyDto
            {
                PolicyArn = policy.Arn,
                PolicyName = policy.PolicyName,
                RiskReason = riskReason,
                Recommendation =
                    "Policy grants broad *:* permissions. Replace with least-privilege actions and resources.",
            });
        }

        return new IamRiskyPoliciesResponseDto
        {
            TotalCustomerPoliciesScanned = policies.Count,
            RiskyPolicyCount = riskyPolicies.Count,
            Policies = riskyPolicies
                .OrderBy(p => p.PolicyName, StringComparer.Ordinal)
                .ToList(),
            ScannedAt = DateTime.UtcNow,
        };
    }

    public async Task<IamOverprivilegedPoliciesResponseDto> GetOverprivilegedCustomPoliciesAsync(
        CancellationToken cancellationToken = default)
    {
        List<ManagedPolicy> policies = await ListCustomerManagedPoliciesAsync(cancellationToken);
        List<IamOverprivilegedPolicyDto> overprivilegedPolicies = new();

        foreach (ManagedPolicy policy in policies)
        {
            if (string.IsNullOrWhiteSpace(policy.Arn) || string.IsNullOrWhiteSpace(policy.PolicyName))
                continue;

            IamOverprivilegedPolicyDto? overprivilegedPolicy =
                await TryMapOverprivilegedPolicyAsync(policy.Arn, policy.PolicyName, cancellationToken);

            if (overprivilegedPolicy is not null)
                overprivilegedPolicies.Add(overprivilegedPolicy);
        }

        List<IamOverprivilegedPolicyDto> orderedPolicies = overprivilegedPolicies
            .OrderBy(policy => GetPrivilegeRank(policy.PrivilegeLevel))
            .ThenBy(policy => policy.PolicyName, StringComparer.Ordinal)
            .ToList();

        _logger.LogInformation(
            "Found {OverprivilegedCount}/{TotalCount} overprivileged customer policies ({CriticalCount} critical, {HighCount} high, {MediumCount} medium)",
            orderedPolicies.Count,
            policies.Count,
            orderedPolicies.Count(policy => string.Equals(policy.PrivilegeLevel, "Critical", StringComparison.OrdinalIgnoreCase)),
            orderedPolicies.Count(policy => string.Equals(policy.PrivilegeLevel, "High", StringComparison.OrdinalIgnoreCase)),
            orderedPolicies.Count(policy => string.Equals(policy.PrivilegeLevel, "Medium", StringComparison.OrdinalIgnoreCase)));

        return new IamOverprivilegedPoliciesResponseDto
        {
            TotalCustomerPoliciesScanned = policies.Count,
            OverprivilegedPolicyCount = orderedPolicies.Count,
            CriticalPolicyCount = orderedPolicies.Count(policy =>
                string.Equals(policy.PrivilegeLevel, "Critical", StringComparison.OrdinalIgnoreCase)),
            HighPolicyCount = orderedPolicies.Count(policy =>
                string.Equals(policy.PrivilegeLevel, "High", StringComparison.OrdinalIgnoreCase)),
            MediumPolicyCount = orderedPolicies.Count(policy =>
                string.Equals(policy.PrivilegeLevel, "Medium", StringComparison.OrdinalIgnoreCase)),
            Policies = orderedPolicies,
            ScannedAt = DateTime.UtcNow,
        };
    }

    private async Task<IamOverprivilegedPolicyDto?> TryMapOverprivilegedPolicyAsync(
        string policyArn,
        string policyName,
        CancellationToken cancellationToken)
    {
        string? policyDocument = await TryGetPolicyDocumentAsync(policyArn, cancellationToken);
        if (string.IsNullOrWhiteSpace(policyDocument))
            return null;

        IReadOnlyList<IamOverprivilegedPolicyAnalyzer.OverprivilegedFinding> findings =
            IamOverprivilegedPolicyAnalyzer.Analyze(policyDocument);

        IamOverprivilegedPolicyAnalyzer.OverprivilegedFinding? worstFinding =
            IamOverprivilegedPolicyAnalyzer.GetWorstFinding(findings);

        if (worstFinding is null)
            return null;

        IReadOnlyList<string> riskyActions = findings
            .SelectMany(finding => finding.RiskyActions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(action => action, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new IamOverprivilegedPolicyDto
        {
            PolicyArn = policyArn,
            PolicyName = policyName,
            PrivilegeLevel = worstFinding.PrivilegeLevel,
            RiskReason = string.Join("; ", findings.Select(finding => finding.RiskReason).Distinct()),
            RiskyActions = riskyActions,
            Recommendation = IamOverprivilegedPolicyAnalyzer.BuildRecommendation(
                worstFinding.PrivilegeLevel,
                riskyActions),
        };
    }

    private static int GetPrivilegeRank(string privilegeLevel) =>
        privilegeLevel switch
        {
            "Critical" => 0,
            "High" => 1,
            "Medium" => 2,
            _ => 3,
        };

    public async Task<IamAdminPrivilegeGrantsResponseDto> GetAdminPrivilegeGrantsAsync(
        CancellationToken cancellationToken = default)
    {
        List<User> users = await ListUsersAsync(cancellationToken);
        List<Group> groups = await ListGroupsAsync(cancellationToken);
        List<Role> roles = await ListRolesAsync(cancellationToken);
        List<IamAdminPrivilegeGrantDto> grants = new();

        foreach (Role role in roles)
        {
            if (string.IsNullOrWhiteSpace(role.RoleName))
                continue;

            await CollectAttachedPolicyGrantsAsync(
                grants,
                principalType: "Role",
                principalName: role.RoleName,
                principalArn: role.Arn,
                attachmentType: "Direct",
                inheritedFromGroup: null,
                listAttachedPolicies: () => ListAttachedRolePoliciesAsync(role.RoleName, cancellationToken));
        }

        foreach (Group group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.GroupName))
                continue;

            await CollectAttachedPolicyGrantsAsync(
                grants,
                principalType: "Group",
                principalName: group.GroupName,
                principalArn: group.Arn,
                attachmentType: "Direct",
                inheritedFromGroup: null,
                listAttachedPolicies: () => ListAttachedGroupPoliciesAsync(group.GroupName, cancellationToken));
        }

        foreach (User user in users)
        {
            if (string.IsNullOrWhiteSpace(user.UserName))
                continue;

            await CollectAttachedPolicyGrantsAsync(
                grants,
                principalType: "User",
                principalName: user.UserName,
                principalArn: user.Arn,
                attachmentType: "Direct",
                inheritedFromGroup: null,
                listAttachedPolicies: () => ListAttachedUserPoliciesAsync(user.UserName, cancellationToken));

            List<Group> userGroups = await ListGroupsForUserAsync(user.UserName, cancellationToken);
            foreach (Group group in userGroups)
            {
                if (string.IsNullOrWhiteSpace(group.GroupName))
                    continue;

                await CollectAttachedPolicyGrantsAsync(
                    grants,
                    principalType: "User",
                    principalName: user.UserName,
                    principalArn: user.Arn,
                    attachmentType: "InheritedFromGroup",
                    inheritedFromGroup: group.GroupName,
                    listAttachedPolicies: () => ListAttachedGroupPoliciesAsync(group.GroupName, cancellationToken));
            }
        }

        List<IamAdminPrivilegeGrantDto> orderedGrants = grants
            .OrderBy(grant => grant.PrincipalType, StringComparer.Ordinal)
            .ThenBy(grant => grant.PrincipalName, StringComparer.Ordinal)
            .ThenBy(grant => grant.PolicyName, StringComparer.Ordinal)
            .ToList();

        _logger.LogInformation(
            "Found {GrantCount} admin privilege grants across {UserCount} users, {GroupCount} groups, {RoleCount} roles",
            orderedGrants.Count,
            users.Count,
            groups.Count,
            roles.Count);

        return new IamAdminPrivilegeGrantsResponseDto
        {
            TotalUsersScanned = users.Count,
            TotalGroupsScanned = groups.Count,
            TotalRolesScanned = roles.Count,
            AdminPrivilegeGrantCount = orderedGrants.Count,
            UsersWithAdminAccess = orderedGrants
                .Where(grant => string.Equals(grant.PrincipalType, "User", StringComparison.Ordinal))
                .Select(grant => grant.PrincipalName)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            GroupsWithAdminAccess = orderedGrants
                .Where(grant => string.Equals(grant.PrincipalType, "Group", StringComparison.Ordinal))
                .Select(grant => grant.PrincipalName)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            RolesWithAdminAccess = orderedGrants
                .Where(grant => string.Equals(grant.PrincipalType, "Role", StringComparison.Ordinal))
                .Select(grant => grant.PrincipalName)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            Grants = orderedGrants,
            ScannedAt = DateTime.UtcNow
        };
    }

    public async Task<IamCrossAccountRolesResponseDto> GetCrossAccountRolesAsync(
        CancellationToken cancellationToken = default)
    {
        string currentAccountId = await ResolveAccountIdAsync(cancellationToken);
        List<Role> roles = await ListRolesAsync(cancellationToken);
        List<IamCrossAccountRoleDto> crossAccountRoles = new();

        foreach (Role role in roles)
        {
            if (string.IsNullOrWhiteSpace(role.RoleName))
                continue;

            string trustDocument = IamPolicyRiskAnalyzer.DecodePolicyDocument(role.AssumeRolePolicyDocument);
            IReadOnlyList<IamTrustPolicyAnalyzer.CrossAccountTrustFinding> findings =
                IamTrustPolicyAnalyzer.AnalyzeCrossAccountTrust(trustDocument, currentAccountId);

            if (findings.Count == 0)
                continue;

            crossAccountRoles.Add(new IamCrossAccountRoleDto
            {
                RoleName = role.RoleName,
                RoleArn = role.Arn ?? string.Empty,
                TrustRiskReason = string.Join("; ", findings.Select(finding => finding.RiskReason).Distinct()),
                TrustedPrincipals = findings
                    .Select(finding => finding.Principal)
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                AllowsAnyPrincipal = findings.Any(finding => finding.AllowsAnyPrincipal),
                Recommendation = IamTrustPolicyAnalyzer.BuildRecommendation(findings)
            });
        }

        List<IamCrossAccountRoleDto> orderedRoles = crossAccountRoles
            .OrderBy(role => role.AllowsAnyPrincipal ? 0 : 1)
            .ThenBy(role => role.RoleName, StringComparer.Ordinal)
            .ToList();

        _logger.LogInformation(
            "Found {CrossAccountCount}/{TotalCount} IAM roles with cross-account or wildcard trust",
            orderedRoles.Count,
            roles.Count);

        return new IamCrossAccountRolesResponseDto
        {
            CurrentAccountId = currentAccountId,
            TotalRolesScanned = roles.Count,
            CrossAccountRolesCount = orderedRoles.Count,
            Roles = orderedRoles,
            ScannedAt = DateTime.UtcNow
        };
    }

    public async Task<IamInlinePoliciesResponseDto> GetInlinePoliciesAsync(
        CancellationToken cancellationToken = default)
    {
        List<User> users = await ListUsersAsync(cancellationToken);
        List<Group> groups = await ListGroupsAsync(cancellationToken);
        List<Role> roles = await ListRolesAsync(cancellationToken);
        List<IamInlinePolicyDto> inlinePolicies = new();

        foreach (Role role in roles)
        {
            if (string.IsNullOrWhiteSpace(role.RoleName))
                continue;

            await CollectInlinePoliciesAsync(
                inlinePolicies,
                principalType: "Role",
                principalName: role.RoleName,
                principalArn: role.Arn,
                listPolicyNames: () => ListInlineRolePolicyNamesAsync(role.RoleName, cancellationToken),
                getPolicyDocument: policyName =>
                    GetInlineRolePolicyDocumentAsync(role.RoleName, policyName, cancellationToken));
        }

        foreach (Group group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.GroupName))
                continue;

            await CollectInlinePoliciesAsync(
                inlinePolicies,
                principalType: "Group",
                principalName: group.GroupName,
                principalArn: group.Arn,
                listPolicyNames: () => ListInlineGroupPolicyNamesAsync(group.GroupName, cancellationToken),
                getPolicyDocument: policyName =>
                    GetInlineGroupPolicyDocumentAsync(group.GroupName, policyName, cancellationToken));
        }

        foreach (User user in users)
        {
            if (string.IsNullOrWhiteSpace(user.UserName))
                continue;

            await CollectInlinePoliciesAsync(
                inlinePolicies,
                principalType: "User",
                principalName: user.UserName,
                principalArn: user.Arn,
                listPolicyNames: () => ListInlineUserPolicyNamesAsync(user.UserName, cancellationToken),
                getPolicyDocument: policyName =>
                    GetInlineUserPolicyDocumentAsync(user.UserName, policyName, cancellationToken));
        }

        List<IamInlinePolicyDto> orderedPolicies = inlinePolicies
            .OrderByDescending(policy => policy.HasWildcardAdminRisk)
            .ThenBy(policy => policy.PrincipalType, StringComparer.Ordinal)
            .ThenBy(policy => policy.PrincipalName, StringComparer.Ordinal)
            .ThenBy(policy => policy.PolicyName, StringComparer.Ordinal)
            .ToList();

        int riskyCount = orderedPolicies.Count(policy => policy.HasWildcardAdminRisk);

        _logger.LogInformation(
            "Found {InlineCount} inline policies ({RiskyCount} with wildcard admin risk) across {UserCount} users, {GroupCount} groups, {RoleCount} roles",
            orderedPolicies.Count,
            riskyCount,
            users.Count,
            groups.Count,
            roles.Count);

        return new IamInlinePoliciesResponseDto
        {
            TotalUsersScanned = users.Count,
            TotalGroupsScanned = groups.Count,
            TotalRolesScanned = roles.Count,
            InlinePolicyCount = orderedPolicies.Count,
            RiskyInlinePolicyCount = riskyCount,
            Policies = orderedPolicies,
            ScannedAt = DateTime.UtcNow
        };
    }

    private static async Task CollectInlinePoliciesAsync(
        List<IamInlinePolicyDto> policies,
        string principalType,
        string principalName,
        string? principalArn,
        Func<Task<List<string>>> listPolicyNames,
        Func<string, Task<string?>> getPolicyDocument)
    {
        List<string> policyNames = await listPolicyNames();

        foreach (string policyName in policyNames)
        {
            if (string.IsNullOrWhiteSpace(policyName))
                continue;

            string? policyDocument = await getPolicyDocument(policyName);
            bool hasWildcardAdminRisk = IamPolicyRiskAnalyzer.TryGetWildcardAdminRisk(
                policyDocument ?? string.Empty,
                out string riskReason);

            policies.Add(new IamInlinePolicyDto
            {
                PrincipalType = principalType,
                PrincipalName = principalName,
                PrincipalArn = principalArn,
                PolicyName = policyName,
                HasWildcardAdminRisk = hasWildcardAdminRisk,
                RiskReason = hasWildcardAdminRisk ? riskReason : null,
                Recommendation = BuildInlinePolicyRecommendation(hasWildcardAdminRisk)
            });
        }
    }

    private static string BuildInlinePolicyRecommendation(bool hasWildcardAdminRisk) =>
        hasWildcardAdminRisk
            ? "Replace wildcard allow with least-privilege actions and resources, or migrate permissions to a managed policy."
            : "Review inline policy permissions regularly. Prefer managed policies for reuse and auditability.";

    private async Task<List<string>> ListInlineUserPolicyNamesAsync(
        string userName,
        CancellationToken cancellationToken) =>
        await ListInlinePolicyNamesAsync(
            marker => _iamClient.ListUserPoliciesAsync(
                new ListUserPoliciesRequest { UserName = userName, Marker = marker },
                cancellationToken),
            response => response.PolicyNames ?? [],
            response => response.IsTruncated == true ? response.Marker : null,
            cancellationToken);

    private async Task<List<string>> ListInlineGroupPolicyNamesAsync(
        string groupName,
        CancellationToken cancellationToken) =>
        await ListInlinePolicyNamesAsync(
            marker => _iamClient.ListGroupPoliciesAsync(
                new ListGroupPoliciesRequest { GroupName = groupName, Marker = marker },
                cancellationToken),
            response => response.PolicyNames ?? [],
            response => response.IsTruncated == true ? response.Marker : null,
            cancellationToken);

    private async Task<List<string>> ListInlineRolePolicyNamesAsync(
        string roleName,
        CancellationToken cancellationToken) =>
        await ListInlinePolicyNamesAsync(
            marker => _iamClient.ListRolePoliciesAsync(
                new ListRolePoliciesRequest { RoleName = roleName, Marker = marker },
                cancellationToken),
            response => response.PolicyNames ?? [],
            response => response.IsTruncated == true ? response.Marker : null,
            cancellationToken);

    private static async Task<List<string>> ListInlinePolicyNamesAsync<TResponse>(
        Func<string?, Task<TResponse>> fetchPage,
        Func<TResponse, IEnumerable<string>> getPolicyNames,
        Func<TResponse, string?> getMarker,
        CancellationToken cancellationToken)
    {
        List<string> policyNames = new();
        string? marker = null;

        do
        {
            TResponse response = await fetchPage(marker);
            policyNames.AddRange(getPolicyNames(response));
            marker = getMarker(response);
        }
        while (!string.IsNullOrEmpty(marker));

        return policyNames;
    }

    private async Task<string?> GetInlineUserPolicyDocumentAsync(
        string userName,
        string policyName,
        CancellationToken cancellationToken) =>
        await GetInlinePolicyDocumentAsync(
            () => _iamClient.GetUserPolicyAsync(
                new GetUserPolicyRequest { UserName = userName, PolicyName = policyName },
                cancellationToken),
            response => response.PolicyDocument,
            $"user {userName}",
            policyName,
            cancellationToken);

    private async Task<string?> GetInlineGroupPolicyDocumentAsync(
        string groupName,
        string policyName,
        CancellationToken cancellationToken) =>
        await GetInlinePolicyDocumentAsync(
            () => _iamClient.GetGroupPolicyAsync(
                new GetGroupPolicyRequest { GroupName = groupName, PolicyName = policyName },
                cancellationToken),
            response => response.PolicyDocument,
            $"group {groupName}",
            policyName,
            cancellationToken);

    private async Task<string?> GetInlineRolePolicyDocumentAsync(
        string roleName,
        string policyName,
        CancellationToken cancellationToken) =>
        await GetInlinePolicyDocumentAsync(
            () => _iamClient.GetRolePolicyAsync(
                new GetRolePolicyRequest { RoleName = roleName, PolicyName = policyName },
                cancellationToken),
            response => response.PolicyDocument,
            $"role {roleName}",
            policyName,
            cancellationToken);

    private async Task<string?> GetInlinePolicyDocumentAsync<TResponse>(
        Func<Task<TResponse>> fetchPolicy,
        Func<TResponse, string?> getDocument,
        string principalDescription,
        string policyName,
        CancellationToken cancellationToken)
    {
        try
        {
            TResponse response = await fetchPolicy();
            return IamPolicyRiskAnalyzer.DecodePolicyDocument(getDocument(response));
        }
        catch (AmazonIdentityManagementServiceException ex)
        {
            _logger.LogWarning(
                ex,
                "Could not read inline policy {PolicyName} for {PrincipalDescription}",
                policyName,
                principalDescription);
            return null;
        }
    }

    public async Task<IamRootAccountStatusResponseDto> GetRootAccountStatusAsync(
        CancellationToken cancellationToken = default)
    {
        string accountId = await ResolveAccountIdAsync(cancellationToken);

        GetAccountSummaryResponse summaryResponse = await _iamClient.GetAccountSummaryAsync(cancellationToken);
        IReadOnlyDictionary<string, int> summary = summaryResponse.SummaryMap ?? new Dictionary<string, int>();

        bool mfaEnabled = GetSummaryFlag(summary, "AccountMFAEnabled");
        bool hasActiveAccessKeys = GetSummaryFlag(summary, "AccountAccessKeysPresent");
        bool hasSigningCertificate = GetSummaryFlag(summary, "AccountSigningCertificatesPresent");

        bool passwordEnabled = false;
        bool accessKey1Active = hasActiveAccessKeys;
        bool accessKey2Active = false;
        DateTime? passwordLastUsed = null;
        DateTime? accessKey1LastUsed = null;
        DateTime? accessKey2LastUsed = null;
        bool credentialReportAvailable = false;

        if (await TryGetRootCredentialDetailsAsync(cancellationToken) is IamCredentialReportParser.RootAccountCredentialDetails details)
        {
            credentialReportAvailable = true;
            mfaEnabled = details.MfaActive;
            passwordEnabled = details.PasswordEnabled;
            accessKey1Active = details.AccessKey1Active;
            accessKey2Active = details.AccessKey2Active;
            hasActiveAccessKeys = details.AccessKey1Active || details.AccessKey2Active;
            hasSigningCertificate = details.Cert1Active || details.Cert2Active;
            passwordLastUsed = details.PasswordLastUsed;
            accessKey1LastUsed = details.AccessKey1LastUsed;
            accessKey2LastUsed = details.AccessKey2LastUsed;
        }

        IReadOnlyList<string> riskReasons = IamCredentialReportParser.BuildRiskReasons(
            mfaEnabled,
            hasActiveAccessKeys,
            hasSigningCertificate);

        _logger.LogInformation(
            "Root account status for {AccountId}: MFA={MfaEnabled}, accessKeys={HasAccessKeys}, risks={RiskCount}",
            accountId,
            mfaEnabled,
            hasActiveAccessKeys,
            riskReasons.Count);

        return new IamRootAccountStatusResponseDto
        {
            AccountId = accountId,
            MfaEnabled = mfaEnabled,
            HasActiveAccessKeys = hasActiveAccessKeys,
            HasSigningCertificate = hasSigningCertificate,
            PasswordEnabled = passwordEnabled,
            AccessKey1Active = accessKey1Active,
            AccessKey2Active = accessKey2Active,
            PasswordLastUsed = passwordLastUsed,
            AccessKey1LastUsed = accessKey1LastUsed,
            AccessKey2LastUsed = accessKey2LastUsed,
            CredentialReportAvailable = credentialReportAvailable,
            IsCompliant = riskReasons.Count == 0,
            RiskReasons = riskReasons,
            Recommendation = IamCredentialReportParser.BuildRecommendation(riskReasons),
            ScannedAt = DateTime.UtcNow
        };
    }

    private static bool GetSummaryFlag(IReadOnlyDictionary<string, int> summary, string key) =>
        summary.TryGetValue(key, out int value) && value == 1;

    private async Task<IamCredentialReportParser.RootAccountCredentialDetails?> TryGetRootCredentialDetailsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await _iamClient.GenerateCredentialReportAsync(new GenerateCredentialReportRequest(), cancellationToken);
        }
        catch (AmazonIdentityManagementServiceException ex)
        {
            _logger.LogDebug(ex, "GenerateCredentialReport returned an expected error; continuing to poll report");
        }

        const int maxAttempts = 20;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                GetCredentialReportResponse reportResponse =
                    await _iamClient.GetCredentialReportAsync(cancellationToken);

                byte[]? content = await ReadReportContentAsync(reportResponse.Content, cancellationToken);
                if (IamCredentialReportParser.TryParseRootAccountDetails(
                        content,
                        out IamCredentialReportParser.RootAccountCredentialDetails details))
                {
                    return details;
                }

                return null;
            }
            catch (AmazonIdentityManagementServiceException ex) when (IsCredentialReportPending(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken);
            }
        }

        _logger.LogWarning("Timed out waiting for IAM credential report");
        return null;
    }

    private static bool IsCredentialReportPending(AmazonIdentityManagementServiceException exception) =>
        string.Equals(exception.ErrorCode, "ReportInProgress", StringComparison.Ordinal) ||
        string.Equals(exception.ErrorCode, "ReportNotPresent", StringComparison.Ordinal);

    private static async Task<byte[]?> ReadReportContentAsync(
        Stream? content,
        CancellationToken cancellationToken)
    {
        if (content is null)
            return null;

        using MemoryStream memoryStream = new();
        await content.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    private async Task<string> ResolveAccountIdAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accountId))
            return _accountId;

        using AmazonSecurityTokenServiceClient stsClient = new();
        GetCallerIdentityResponse identity = await stsClient.GetCallerIdentityAsync(
            new GetCallerIdentityRequest(),
            cancellationToken);

        _accountId = identity.Account;
        return _accountId;
    }

    private sealed record AttachedPolicyInfo(string? PolicyArn, string? PolicyName);

    private static async Task CollectAttachedPolicyGrantsAsync(
        List<IamAdminPrivilegeGrantDto> grants,
        string principalType,
        string principalName,
        string? principalArn,
        string attachmentType,
        string? inheritedFromGroup,
        Func<Task<List<AttachedPolicyInfo>>> listAttachedPolicies)
    {
        List<AttachedPolicyInfo> attachedPolicies = await listAttachedPolicies();

        foreach (AttachedPolicyInfo attachedPolicy in attachedPolicies)
        {
            if (!IamAdminPolicyClassifier.TryGetPrivilegeLevel(
                    attachedPolicy.PolicyArn,
                    attachedPolicy.PolicyName,
                    out string privilegeLevel))
            {
                continue;
            }

            grants.Add(new IamAdminPrivilegeGrantDto
            {
                PrincipalType = principalType,
                PrincipalName = principalName,
                PrincipalArn = principalArn,
                PolicyArn = attachedPolicy.PolicyArn ?? string.Empty,
                PolicyName = attachedPolicy.PolicyName ?? string.Empty,
                PrivilegeLevel = privilegeLevel,
                AttachmentType = attachmentType,
                InheritedFromGroup = inheritedFromGroup,
                Recommendation = IamAdminPolicyClassifier.BuildRecommendation(privilegeLevel)
            });
        }
    }

    private async Task<List<AttachedPolicyInfo>> ListAttachedUserPoliciesAsync(
        string userName,
        CancellationToken cancellationToken) =>
        await ListAttachedPoliciesAsync(
            marker => _iamClient.ListAttachedUserPoliciesAsync(
                new ListAttachedUserPoliciesRequest { UserName = userName, Marker = marker },
                cancellationToken),
            response => response.AttachedPolicies?.Select(MapAttachedPolicy) ?? [],
            response => response.IsTruncated == true ? response.Marker : null,
            cancellationToken);

    private async Task<List<AttachedPolicyInfo>> ListAttachedGroupPoliciesAsync(
        string groupName,
        CancellationToken cancellationToken) =>
        await ListAttachedPoliciesAsync(
            marker => _iamClient.ListAttachedGroupPoliciesAsync(
                new ListAttachedGroupPoliciesRequest { GroupName = groupName, Marker = marker },
                cancellationToken),
            response => response.AttachedPolicies?.Select(MapAttachedPolicy) ?? [],
            response => response.IsTruncated == true ? response.Marker : null,
            cancellationToken);

    private async Task<List<AttachedPolicyInfo>> ListAttachedRolePoliciesAsync(
        string roleName,
        CancellationToken cancellationToken) =>
        await ListAttachedPoliciesAsync(
            marker => _iamClient.ListAttachedRolePoliciesAsync(
                new ListAttachedRolePoliciesRequest { RoleName = roleName, Marker = marker },
                cancellationToken),
            response => response.AttachedPolicies?.Select(MapAttachedPolicy) ?? [],
            response => response.IsTruncated == true ? response.Marker : null,
            cancellationToken);

    private static AttachedPolicyInfo MapAttachedPolicy(AttachedPolicyType policy) =>
        new AttachedPolicyInfo(policy.PolicyArn, policy.PolicyName);

    private static async Task<List<AttachedPolicyInfo>> ListAttachedPoliciesAsync<TResponse>(
        Func<string?, Task<TResponse>> fetchPage,
        Func<TResponse, IEnumerable<AttachedPolicyInfo>> getPolicies,
        Func<TResponse, string?> getMarker,
        CancellationToken cancellationToken)
    {
        List<AttachedPolicyInfo> policies = new();
        string? marker = null;

        do
        {
            TResponse response = await fetchPage(marker);
            policies.AddRange(getPolicies(response));

            marker = getMarker(response);
        }
        while (!string.IsNullOrEmpty(marker));

        return policies;
    }

    private async Task<List<Group>> ListGroupsAsync(CancellationToken cancellationToken)
    {
        List<Group> groups = new();
        string? marker = null;

        do
        {
            ListGroupsResponse response = await _iamClient.ListGroupsAsync(
                new ListGroupsRequest { Marker = marker },
                cancellationToken);

            groups.AddRange(response.Groups ?? []);
            marker = response.IsTruncated == true ? response.Marker : null;
        }
        while (!string.IsNullOrEmpty(marker));

        return groups;
    }

    private async Task<List<Role>> ListRolesAsync(CancellationToken cancellationToken)
    {
        List<Role> roles = new();
        string? marker = null;

        do
        {
            ListRolesResponse response = await _iamClient.ListRolesAsync(
                new ListRolesRequest { Marker = marker },
                cancellationToken);

            roles.AddRange(response.Roles ?? []);
            marker = response.IsTruncated == true ? response.Marker : null;
        }
        while (!string.IsNullOrEmpty(marker));

        return roles;
    }

    private async Task<List<Group>> ListGroupsForUserAsync(
        string userName,
        CancellationToken cancellationToken)
    {
        List<Group> groups = new();
        string? marker = null;

        do
        {
            ListGroupsForUserResponse response = await _iamClient.ListGroupsForUserAsync(
                new ListGroupsForUserRequest { UserName = userName, Marker = marker },
                cancellationToken);

            groups.AddRange(response.Groups ?? []);
            marker = response.IsTruncated == true ? response.Marker : null;
        }
        while (!string.IsNullOrEmpty(marker));

        return groups;
    }

    private async Task<string?> ResolveUserEmailAsync(
        string userName,
        CancellationToken cancellationToken)
    {
        if (userName.Contains('@', StringComparison.Ordinal))
            return userName;

        try
        {
            ListUserTagsResponse tagsResponse = await _iamClient.ListUserTagsAsync(
                new ListUserTagsRequest { UserName = userName },
                cancellationToken);

            string? fromTag = tagsResponse.Tags?
                .FirstOrDefault(tag =>
                    string.Equals(tag.Key, "email", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tag.Key, "mail", StringComparison.OrdinalIgnoreCase))
                ?.Value;

            return string.IsNullOrWhiteSpace(fromTag) ? null : fromTag.Trim();
        }
        catch (AmazonIdentityManagementServiceException ex)
        {
            _logger.LogWarning(ex, "Could not list tags for IAM user {UserName}", userName);
            return null;
        }
    }

    private async Task<bool> UserHasConsoleAccessAsync(string userName, CancellationToken cancellationToken)
    {
        try
        {
            await _iamClient.GetLoginProfileAsync(
                new GetLoginProfileRequest { UserName = userName },
                cancellationToken);
            return true;
        }
        catch (NoSuchEntityException)
        {
            return false;
        }
    }

    private async Task<bool> UserHasMfaDeviceAsync(string userName, CancellationToken cancellationToken)
    {
        ListMFADevicesResponse response = await _iamClient.ListMFADevicesAsync(
            new ListMFADevicesRequest { UserName = userName },
            cancellationToken);

        return (response.MFADevices?.Count ?? 0) > 0;
    }

    private async Task<List<ManagedPolicy>> ListCustomerManagedPoliciesAsync(
        CancellationToken cancellationToken)
    {
        List<ManagedPolicy> policies = new();
        string? marker = null;

        do
        {
            ListPoliciesResponse response = await _iamClient.ListPoliciesAsync(
                new ListPoliciesRequest { Scope = PolicyScopeType.Local, Marker = marker },
                cancellationToken);

            policies.AddRange(response.Policies ?? []);
            marker = response.IsTruncated == true ? response.Marker : null;
        }
        while (!string.IsNullOrEmpty(marker));

        return policies;
    }

    private async Task<string?> TryGetPolicyWildcardRiskAsync(
        string policyArn,
        CancellationToken cancellationToken)
    {
        string? document = await TryGetPolicyDocumentAsync(policyArn, cancellationToken);
        if (string.IsNullOrWhiteSpace(document))
            return null;

        return IamPolicyRiskAnalyzer.TryGetWildcardAdminRisk(document, out string riskReason)
            ? riskReason
            : null;
    }

    private async Task<string?> TryGetPolicyDocumentAsync(
        string policyArn,
        CancellationToken cancellationToken)
    {
        try
        {
            GetPolicyResponse policy = await _iamClient.GetPolicyAsync(
                new GetPolicyRequest { PolicyArn = policyArn },
                cancellationToken);

            if (string.IsNullOrWhiteSpace(policy.Policy?.DefaultVersionId))
                return null;

            GetPolicyVersionResponse version = await _iamClient.GetPolicyVersionAsync(
                new GetPolicyVersionRequest
                {
                    PolicyArn = policyArn,
                    VersionId = policy.Policy.DefaultVersionId,
                },
                cancellationToken);

            return IamPolicyRiskAnalyzer.DecodePolicyDocument(version.PolicyVersion?.Document);
        }
        catch (AmazonIdentityManagementServiceException ex)
        {
            _logger.LogWarning(ex, "Could not evaluate policy {PolicyArn}", policyArn);
            return null;
        }
    }

    private string BuildRecommendation(
        string status,
        bool needsRotation,
        int ageInDays,
        DateTime? lastUsedDate,
        bool isNeverUsed)
    {
        if (status == StatusType.Inactive)
            return "Access key is inactive. Remove unused keys to reduce credential sprawl.";

        if (needsRotation)
            return $"Access key is {ageInDays} days old. Rotate active keys at least every {_rotationMaxAgeDays} days.";

        if (isNeverUsed)
            return "Active access key has no recorded usage. Verify if it is still required or deactivate it.";

        return "Access key is within the recommended rotation window.";
    }

    private static class StatusType
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";
    }
}
