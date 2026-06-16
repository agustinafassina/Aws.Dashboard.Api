using System.Text.Json;

namespace Aws.Repository.Helpers;

internal static class IamTrustPolicyAnalyzer
{
    internal sealed record CrossAccountTrustFinding(
        string RiskReason,
        string Principal,
        bool AllowsAnyPrincipal);

    public static IReadOnlyList<CrossAccountTrustFinding> AnalyzeCrossAccountTrust(
        string trustPolicyJson,
        string currentAccountId)
    {
        List<CrossAccountTrustFinding> findings = new();

        if (string.IsNullOrWhiteSpace(trustPolicyJson))
            return findings;

        try
        {
            using JsonDocument document = JsonDocument.Parse(trustPolicyJson);

            if (!document.RootElement.TryGetProperty("Statement", out JsonElement statementElement))
                return findings;

            if (statementElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement statement in statementElement.EnumerateArray())
                    AnalyzeStatement(statement, currentAccountId, findings);
            }
            else
            {
                AnalyzeStatement(statementElement, currentAccountId, findings);
            }
        }
        catch (JsonException)
        {
            return findings;
        }

        return findings
            .DistinctBy(finding => $"{finding.RiskReason}|{finding.Principal}|{finding.AllowsAnyPrincipal}")
            .ToList();
    }

    private static void AnalyzeStatement(
        JsonElement statement,
        string currentAccountId,
        List<CrossAccountTrustFinding> findings)
    {
        if (statement.ValueKind != JsonValueKind.Object)
            return;

        if (statement.TryGetProperty("Effect", out JsonElement effect) &&
            !string.Equals(effect.GetString(), "Allow", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!StatementAllowsAssumeRole(statement))
            return;

        if (statement.TryGetProperty("Principal", out JsonElement principal))
            AnalyzePrincipal(principal, currentAccountId, findings);
    }

    private static bool StatementAllowsAssumeRole(JsonElement statement)
    {
        if (!statement.TryGetProperty("Action", out JsonElement actionElement))
            return true;

        return ActionIncludesAssumeRole(actionElement);
    }

    private static bool ActionIncludesAssumeRole(JsonElement actionElement)
    {
        if (actionElement.ValueKind == JsonValueKind.String)
            return IsAssumeRoleAction(actionElement.GetString());

        if (actionElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement action in actionElement.EnumerateArray())
            {
                if (action.ValueKind == JsonValueKind.String && IsAssumeRoleAction(action.GetString()))
                    return true;
            }
        }

        return false;
    }

    private static bool IsAssumeRoleAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return false;

        if (string.Equals(action, "*", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, "sts:*", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return action.StartsWith("sts:Assume", StringComparison.OrdinalIgnoreCase);
    }

    private static void AnalyzePrincipal(
        JsonElement principal,
        string currentAccountId,
        List<CrossAccountTrustFinding> findings)
    {
        if (principal.ValueKind == JsonValueKind.String)
        {
            if (string.Equals(principal.GetString(), "*", StringComparison.Ordinal))
            {
                findings.Add(new CrossAccountTrustFinding(
                    "Trust policy allows any principal",
                    "*",
                    AllowsAnyPrincipal: true));
            }

            return;
        }

        if (principal.ValueKind != JsonValueKind.Object)
            return;

        if (principal.TryGetProperty("AWS", out JsonElement awsPrincipal))
            AnalyzeAwsPrincipal(awsPrincipal, currentAccountId, findings);
    }

    private static void AnalyzeAwsPrincipal(
        JsonElement awsPrincipal,
        string currentAccountId,
        List<CrossAccountTrustFinding> findings)
    {
        if (awsPrincipal.ValueKind == JsonValueKind.String)
        {
            AnalyzeAwsPrincipalValue(awsPrincipal.GetString(), currentAccountId, findings);
            return;
        }

        if (awsPrincipal.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in awsPrincipal.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String)
                    AnalyzeAwsPrincipalValue(entry.GetString(), currentAccountId, findings);
            }
        }
    }

    private static void AnalyzeAwsPrincipalValue(
        string? principalValue,
        string currentAccountId,
        List<CrossAccountTrustFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(principalValue))
            return;

        if (string.Equals(principalValue, "*", StringComparison.Ordinal))
        {
            findings.Add(new CrossAccountTrustFinding(
                "Trust policy allows any AWS principal",
                "*",
                AllowsAnyPrincipal: true));
            return;
        }

        string? trustedAccountId = TryExtractAccountId(principalValue);
        if (string.IsNullOrWhiteSpace(trustedAccountId))
            return;

        if (string.Equals(trustedAccountId, currentAccountId, StringComparison.Ordinal))
            return;

        findings.Add(new CrossAccountTrustFinding(
            $"Trust policy allows external AWS account {trustedAccountId}",
            principalValue.Trim(),
            AllowsAnyPrincipal: false));
    }

    internal static string? TryExtractAccountId(string principalValue)
    {
        if (principalValue.StartsWith("arn:", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = principalValue.Split(':');
            return parts.Length >= 5 ? parts[4] : null;
        }

        return principalValue.All(char.IsDigit) && principalValue.Length >= 12
            ? principalValue
            : null;
    }

    public static string BuildRecommendation(IReadOnlyList<CrossAccountTrustFinding> findings)
    {
        if (findings.Any(finding => finding.AllowsAnyPrincipal))
        {
            return "Remove wildcard principals from the trust policy. Restrict sts:AssumeRole to specific AWS account IDs or roles and add conditions such as ExternalId or aws:SourceAccount.";
        }

        return "Review cross-account trust relationships. Confirm the external account is required, scope permissions with least privilege, and enforce ExternalId or source-account conditions.";
    }
}
