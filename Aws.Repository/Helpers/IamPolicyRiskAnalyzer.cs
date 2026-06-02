using System.Net;
using System.Text.Json;

namespace Aws.Repository.Helpers;

internal static class IamPolicyRiskAnalyzer
{
    public static bool TryGetWildcardAdminRisk(string policyDocumentJson, out string riskReason)
    {
        riskReason = string.Empty;

        if (string.IsNullOrWhiteSpace(policyDocumentJson))
            return false;

        try
        {
            using JsonDocument document = JsonDocument.Parse(policyDocumentJson);

            if (!document.RootElement.TryGetProperty("Statement", out JsonElement statementElement))
                return false;

            if (statementElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement statement in statementElement.EnumerateArray())
                {
                    if (StatementGrantsWildcardAdmin(statement, out riskReason))
                        return true;
                }

                return false;
            }

            return StatementGrantsWildcardAdmin(statementElement, out riskReason);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool StatementGrantsWildcardAdmin(JsonElement statement, out string riskReason)
    {
        riskReason = string.Empty;

        if (statement.ValueKind != JsonValueKind.Object)
            return false;

        if (statement.TryGetProperty("Effect", out JsonElement effect) &&
            !string.Equals(effect.GetString(), "Allow", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!ElementContainsWildcard(statement, "Action") ||
            !ElementContainsWildcard(statement, "Resource"))
        {
            return false;
        }

        riskReason = "Allows Action * on Resource *";
        return true;
    }

    private static bool ElementContainsWildcard(JsonElement statement, string propertyName)
    {
        if (!statement.TryGetProperty(propertyName, out JsonElement value))
            return false;

        if (value.ValueKind == JsonValueKind.String)
            return string.Equals(value.GetString(), "*", StringComparison.Ordinal);

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String &&
                    string.Equals(item.GetString(), "*", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static string DecodePolicyDocument(string? encodedDocument) =>
        string.IsNullOrEmpty(encodedDocument) ? string.Empty : WebUtility.UrlDecode(encodedDocument);
}
