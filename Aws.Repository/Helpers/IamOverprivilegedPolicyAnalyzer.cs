using System.Text.Json;

namespace Aws.Repository.Helpers;

internal static class IamOverprivilegedPolicyAnalyzer
{
    internal sealed record OverprivilegedFinding(
        string PrivilegeLevel,
        string RiskReason,
        IReadOnlyList<string> RiskyActions);

    private static readonly HashSet<string> HighRiskServiceWildcardActions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "iam:*",
            "s3:*",
            "ec2:*",
            "kms:*",
            "lambda:*",
            "rds:*",
            "dynamodb:*",
            "sts:*",
            "cloudformation:*",
            "organizations:*",
        };

    public static IReadOnlyList<OverprivilegedFinding> Analyze(string policyDocumentJson)
    {
        List<OverprivilegedFinding> findings = new();

        if (string.IsNullOrWhiteSpace(policyDocumentJson))
            return findings;

        try
        {
            using JsonDocument document = JsonDocument.Parse(policyDocumentJson);

            if (!document.RootElement.TryGetProperty("Statement", out JsonElement statementElement))
                return findings;

            if (statementElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement statement in statementElement.EnumerateArray())
                    AnalyzeStatement(statement, findings);
            }
            else
            {
                AnalyzeStatement(statementElement, findings);
            }
        }
        catch (JsonException)
        {
            return findings;
        }

        return findings
            .DistinctBy(finding => $"{finding.PrivilegeLevel}|{finding.RiskReason}")
            .OrderBy(finding => GetPrivilegeRank(finding.PrivilegeLevel))
            .ThenBy(finding => finding.RiskReason, StringComparer.Ordinal)
            .ToList();
    }

    public static OverprivilegedFinding? GetWorstFinding(IReadOnlyList<OverprivilegedFinding> findings) =>
        findings.Count == 0 ? null : findings[0];

    public static string BuildRecommendation(string privilegeLevel, IReadOnlyList<string> riskyActions)
    {
        if (string.Equals(privilegeLevel, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            return "Remove wildcard admin permissions. Scope actions and resources to the minimum required for each workload.";
        }

        if (riskyActions.Any(action => action.StartsWith("iam:", StringComparison.OrdinalIgnoreCase)))
        {
            return "Replace broad IAM permissions with specific actions on named roles, users, or groups. Avoid iam:* on wildcard resources.";
        }

        if (riskyActions.Any(action => action.StartsWith("s3:", StringComparison.OrdinalIgnoreCase)))
        {
            return "Restrict S3 permissions to required buckets and object prefixes instead of s3:* on all resources.";
        }

        return "Replace service-wide wildcard permissions with least-privilege actions and scoped resource ARNs. Add conditions where possible.";
    }

    private static void AnalyzeStatement(JsonElement statement, List<OverprivilegedFinding> findings)
    {
        if (statement.ValueKind != JsonValueKind.Object)
            return;

        if (statement.TryGetProperty("Effect", out JsonElement effect) &&
            !string.Equals(effect.GetString(), "Allow", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (HasRestrictiveCondition(statement))
            return;

        List<string> actions = GetStringValues(statement, "Action");
        List<string> resources = GetStringValues(statement, "Resource");

        if (actions.Count == 0 || resources.Count == 0)
            return;

        bool actionIsWildcard = actions.Any(IsWildcard);
        bool resourceIsWildcard = resources.Any(IsWildcard);

        if (actionIsWildcard && resourceIsWildcard)
        {
            findings.Add(new OverprivilegedFinding(
                "Critical",
                "Allows Action * on Resource *",
                ["*:*"]));
            return;
        }

        List<string> serviceWildcardActions = actions
            .Where(HighRiskServiceWildcardActions.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (serviceWildcardActions.Count > 0 && resourceIsWildcard)
        {
            findings.Add(new OverprivilegedFinding(
                "High",
                $"Allows service-wide actions on all resources: {string.Join(", ", serviceWildcardActions)}",
                serviceWildcardActions));
        }

        if (actionIsWildcard && !resourceIsWildcard)
        {
            findings.Add(new OverprivilegedFinding(
                "Medium",
                "Allows Action * on specific resources",
                ["*"]));
        }
    }

    private static bool HasRestrictiveCondition(JsonElement statement) =>
        statement.TryGetProperty("Condition", out JsonElement condition) &&
        condition.ValueKind == JsonValueKind.Object &&
        condition.EnumerateObject().Any();

    private static List<string> GetStringValues(JsonElement statement, string propertyName)
    {
        if (!statement.TryGetProperty(propertyName, out JsonElement value))
            return [];

        if (value.ValueKind == JsonValueKind.String)
        {
            string? singleValue = value.GetString();
            return string.IsNullOrWhiteSpace(singleValue) ? [] : [singleValue];
        }

        if (value.ValueKind != JsonValueKind.Array)
            return [];

        List<string> values = new();
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                values.Add(item.GetString()!);
        }

        return values;
    }

    private static bool IsWildcard(string value) =>
        string.Equals(value, "*", StringComparison.Ordinal);

    private static int GetPrivilegeRank(string privilegeLevel) =>
        privilegeLevel switch
        {
            "Critical" => 0,
            "High" => 1,
            "Medium" => 2,
            _ => 3,
        };
}
