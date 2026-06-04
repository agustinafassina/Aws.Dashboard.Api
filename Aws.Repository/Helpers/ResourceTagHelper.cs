using Amazon.EC2.Model;

namespace Aws.Repository.Helpers;

internal static class ResourceTagHelper
{
    public static bool HasRequiredTag(IEnumerable<Tag>? tags, string requiredTagKey)
    {
        if (tags is null)
            return false;

        return tags.Any(tag =>
            string.Equals(tag.Key, requiredTagKey, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(tag.Value));
    }

    public static bool HasRequiredTag(
        IEnumerable<Amazon.RDS.Model.Tag>? tags,
        string requiredTagKey)
    {
        if (tags is null)
            return false;

        return tags.Any(tag =>
            string.Equals(tag.Key, requiredTagKey, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(tag.Value));
    }

    public static bool HasRequiredTag(
        IEnumerable<Amazon.S3.Model.Tag>? tags,
        string requiredTagKey)
    {
        if (tags is null)
            return false;

        return tags.Any(tag =>
            string.Equals(tag.Key, requiredTagKey, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(tag.Value));
    }

    public static string? GetTagValue(IEnumerable<Tag>? tags, string tagKey)
    {
        if (tags is null)
            return null;

        return tags
            .FirstOrDefault(tag => string.Equals(tag.Key, tagKey, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    public static string? GetTagValue(
        IEnumerable<Amazon.RDS.Model.Tag>? tags,
        string tagKey)
    {
        if (tags is null)
            return null;

        return tags
            .FirstOrDefault(tag => string.Equals(tag.Key, tagKey, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    public static string? GetTagValue(
        IEnumerable<Amazon.S3.Model.Tag>? tags,
        string tagKey)
    {
        if (tags is null)
            return null;

        return tags
            .FirstOrDefault(tag => string.Equals(tag.Key, tagKey, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    public static bool HasRequiredTag(IReadOnlyDictionary<string, string>? tags, string requiredTagKey) =>
        !string.IsNullOrWhiteSpace(GetTagValue(tags, requiredTagKey));

    public static string? GetTagValue(IReadOnlyDictionary<string, string>? tags, string tagKey)
    {
        if (tags is null || tags.Count == 0)
            return null;

        foreach (KeyValuePair<string, string> tag in tags)
        {
            if (string.Equals(tag.Key, tagKey, StringComparison.OrdinalIgnoreCase))
                return tag.Value;
        }

        return null;
    }

    public static bool MatchesTagValue(string? actualValue, string expectedValue) =>
        !string.IsNullOrWhiteSpace(actualValue) &&
        string.Equals(actualValue.Trim(), expectedValue.Trim(), StringComparison.OrdinalIgnoreCase);

    public static string BuildUntaggedRecommendation(string requiredTagKey) =>
        $"Add the '{requiredTagKey}' tag for cost allocation and governance.";
}
