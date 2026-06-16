namespace Aws.Repository.Helpers;

internal static class IamAdminPolicyClassifier
{
    private static readonly Dictionary<string, string> AdminPolicyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AdministratorAccess"] = "Administrator",
        ["PowerUserAccess"] = "PowerUser",
        ["IAMFullAccess"] = "IAMFull"
    };

    public static bool TryGetPrivilegeLevel(string? policyArn, string? policyName, out string privilegeLevel)
    {
        privilegeLevel = string.Empty;

        if (!string.IsNullOrWhiteSpace(policyName) &&
            AdminPolicyNames.TryGetValue(ExtractPolicyName(policyName), out privilegeLevel))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(policyArn))
        {
            string arnPolicyName = ExtractPolicyNameFromArn(policyArn);
            if (AdminPolicyNames.TryGetValue(arnPolicyName, out privilegeLevel))
                return true;
        }

        return false;
    }

    public static string BuildRecommendation(string privilegeLevel) =>
        privilegeLevel switch
        {
            "Administrator" =>
                "Remove AdministratorAccess and replace it with scoped, least-privilege policies through dedicated roles or groups.",
            "PowerUser" =>
                "PowerUserAccess is overly broad for day-to-day work. Prefer task-specific managed or customer policies.",
            "IAMFull" =>
                "IAMFullAccess grants full IAM administration. Restrict to break-glass roles and remove from standard users.",
            _ => "Review and replace this high-privilege policy with least-privilege permissions."
        };

    private static string ExtractPolicyName(string policyName)
    {
        int slashIndex = policyName.LastIndexOf('/');
        return slashIndex >= 0 ? policyName[(slashIndex + 1)..] : policyName;
    }

    private static string ExtractPolicyNameFromArn(string policyArn)
    {
        int slashIndex = policyArn.LastIndexOf('/');
        return slashIndex >= 0 ? policyArn[(slashIndex + 1)..] : policyArn;
    }
}
