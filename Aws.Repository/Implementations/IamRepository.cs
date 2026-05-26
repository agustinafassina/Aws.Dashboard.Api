using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aws.Models.Configuration;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;

namespace Aws.Repository.Implementations;

public class IamRepository : IIamRepository
{
    private readonly IAmazonIdentityManagementService _iamClient;
    private readonly ILogger<IamRepository> _logger;
    private readonly int _rotationMaxAgeDays;

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

            users.AddRange(response.Users);
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

            foreach (var metadata in response.AccessKeyMetadata)
            {
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
            Recommendation = BuildRecommendation(status, needsRotation, ageInDays, lastUsedDate)
        };
    }

    private string BuildRecommendation(
        string status,
        bool needsRotation,
        int ageInDays,
        DateTime? lastUsedDate)
    {
        if (status == StatusType.Inactive)
            return "Access key is inactive. Remove unused keys to reduce credential sprawl.";

        if (needsRotation)
            return $"Access key is {ageInDays} days old. Rotate active keys at least every {_rotationMaxAgeDays} days.";

        if (lastUsedDate == null)
            return "Active access key has no recorded usage. Verify if it is still required or deactivate it.";

        return "Access key is within the recommended rotation window.";
    }

    private static class StatusType
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";
    }
}
