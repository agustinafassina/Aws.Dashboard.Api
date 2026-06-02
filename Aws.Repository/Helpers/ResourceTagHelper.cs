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

    public static string BuildUntaggedRecommendation(string requiredTagKey) =>
        $"Add the '{requiredTagKey}' tag for cost allocation and governance.";
}
