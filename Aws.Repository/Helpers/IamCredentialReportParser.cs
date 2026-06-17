using System.Globalization;
using System.Text;

namespace Aws.Repository.Helpers;

internal static class IamCredentialReportParser
{
    internal sealed record RootAccountCredentialDetails(
        bool PasswordEnabled,
        bool MfaActive,
        bool AccessKey1Active,
        bool AccessKey2Active,
        bool Cert1Active,
        bool Cert2Active,
        DateTime? PasswordLastUsed,
        DateTime? AccessKey1LastUsed,
        DateTime? AccessKey2LastUsed);

    public static bool TryParseRootAccountDetails(byte[]? reportContent, out RootAccountCredentialDetails details)
    {
        details = new RootAccountCredentialDetails(
            PasswordEnabled: false,
            MfaActive: false,
            AccessKey1Active: false,
            AccessKey2Active: false,
            Cert1Active: false,
            Cert2Active: false,
            PasswordLastUsed: null,
            AccessKey1LastUsed: null,
            AccessKey2LastUsed: null);

        if (reportContent is null || reportContent.Length == 0)
            return false;

        string csv = Encoding.UTF8.GetString(reportContent);
        string[] lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string line in lines.Skip(1))
        {
            string[] fields = ParseCsvLine(line);
            if (fields.Length < 15)
                continue;

            if (!string.Equals(fields[0], "<root_account>", StringComparison.Ordinal))
                continue;

            details = new RootAccountCredentialDetails(
                PasswordEnabled: ParseBool(fields[3]),
                MfaActive: ParseBool(fields[7]),
                AccessKey1Active: ParseBool(fields[8]),
                AccessKey2Active: ParseBool(fields[11]),
                Cert1Active: ParseBool(fields[14]),
                Cert2Active: fields.Length > 17 && ParseBool(fields[17]),
                PasswordLastUsed: ParseDate(fields[4]),
                AccessKey1LastUsed: ParseDate(fields[10]),
                AccessKey2LastUsed: ParseDate(fields[13]));

            return true;
        }

        return false;
    }

    public static IReadOnlyList<string> BuildRiskReasons(
        bool mfaEnabled,
        bool hasActiveAccessKeys,
        bool hasSigningCertificate)
    {
        List<string> reasons = new();

        if (!mfaEnabled)
            reasons.Add("Root account MFA is not enabled");

        if (hasActiveAccessKeys)
            reasons.Add("Root account has active access keys");

        if (hasSigningCertificate)
            reasons.Add("Root account has an active signing certificate");

        return reasons;
    }

    public static string BuildRecommendation(IReadOnlyList<string> riskReasons)
    {
        if (riskReasons.Count == 0)
        {
            return "Root account follows recommended security practices. Continue monitoring and avoid creating root access keys.";
        }

        List<string> recommendations = new();

        if (riskReasons.Any(reason => reason.Contains("MFA", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add("Enable hardware or virtual MFA on the root account.");
        }

        if (riskReasons.Any(reason => reason.Contains("access keys", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add("Delete root access keys and use IAM users or roles with least privilege instead.");
        }

        if (riskReasons.Any(reason => reason.Contains("signing certificate", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add("Remove unused root signing certificates.");
        }

        return string.Join(' ', recommendations);
    }

    private static string[] ParseCsvLine(string line)
    {
        List<string> fields = new();
        StringBuilder current = new();
        bool inQuotes = false;

        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];

            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (character == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }

    private static bool ParseBool(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "no_information", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime parsed)
            ? parsed.ToUniversalTime()
            : null;
    }
}
