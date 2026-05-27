using Amazon;
using Amazon.Inspector2;
using Amazon.Inspector2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aws.Models.Configuration;
using Aws.Models.Dto;
using Aws.Repository.Helpers;
using Aws.Repository.Interfaces;

namespace Aws.Repository.Implementations;

public class InspectorRepository : IInspectorRepository
{
    private readonly IAmazonInspector2 _inspectorClient;
    private readonly ILogger<InspectorRepository> _logger;
    private readonly InspectorOptions _options;

    public InspectorRepository(
        IAmazonInspector2 inspectorClient,
        IOptions<InspectorOptions> inspectorOptions,
        ILogger<InspectorRepository> logger)
    {
        _inspectorClient = inspectorClient;
        _logger = logger;
        _options = inspectorOptions.Value;
    }

    public async Task<InspectorVulnerabilitiesResponseDto> GetVulnerabilitiesAsync(
        string region,
        string? resourceType,
        string? severity,
        string? status,
        CancellationToken cancellationToken = default)
    {
        string normalizedRegion = region.Trim();
        IAmazonInspector2 client = GetRegionalClient(normalizedRegion);
        FilterCriteria filterCriteria = BuildFilterCriteria(resourceType, severity, status);

        List<InspectorFindingDto> findings = new();
        string? nextToken = null;
        bool hasMoreFindings = false;

        do
        {
            ListFindingsRequest request = new()
            {
                FilterCriteria = filterCriteria,
                MaxResults = Math.Clamp(_options.PageSize, 1, 100),
                NextToken = nextToken,
                SortCriteria = new SortCriteria
                {
                    Field = SortField.SEVERITY,
                    SortOrder = SortOrder.DESC
                }
            };

            ListFindingsResponse response = await client.ListFindingsAsync(request, cancellationToken);

            foreach (Finding finding in response.Findings ?? [])
            {
                if (findings.Count >= _options.MaxTotalFindings)
                {
                    hasMoreFindings = true;
                    break;
                }

                findings.Add(InspectorFindingMapper.MapFinding(finding));
            }

            if (hasMoreFindings)
                break;

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        if (!hasMoreFindings && !string.IsNullOrEmpty(nextToken))
            hasMoreFindings = true;

        _logger.LogInformation(
            "Found {FindingCount} Inspector findings in region {Region} (resourceType={ResourceType}, severity={Severity}, status={Status})",
            findings.Count,
            normalizedRegion,
            resourceType ?? "all",
            severity ?? "all",
            status ?? "all");

        return new InspectorVulnerabilitiesResponseDto
        {
            Region = normalizedRegion,
            ResourceTypeFilter = resourceType,
            SeverityFilter = severity,
            StatusFilter = status,
            TotalFindings = findings.Count,
            HasMoreFindings = hasMoreFindings,
            Findings = findings,
            ScannedAt = DateTime.UtcNow
        };
    }

    private static FilterCriteria BuildFilterCriteria(
        string? resourceType,
        string? severity,
        string? status)
    {
        FilterCriteria filter = new();

        if (!string.IsNullOrWhiteSpace(resourceType))
        {
            filter.ResourceType =
            [
                new StringFilter
                {
                    Comparison = Amazon.Inspector2.StringComparison.EQUALS,
                    Value = MapResourceType(resourceType)
                }
            ];
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            filter.Severity =
            [
                new StringFilter
                {
                    Comparison = Amazon.Inspector2.StringComparison.EQUALS,
                    Value = severity.Trim().ToUpperInvariant()
                }
            ];
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter.FindingStatus =
            [
                new StringFilter
                {
                    Comparison = Amazon.Inspector2.StringComparison.EQUALS,
                    Value = status.Trim().ToUpperInvariant()
                }
            ];
        }

        return filter;
    }

    internal static string MapResourceType(string resourceType) =>
        resourceType.Trim().ToLowerInvariant() switch
        {
            "ec2" => "AWS_EC2_INSTANCE",
            "lambda" => "AWS_LAMBDA_FUNCTION",
            "ecr" => "AWS_ECR_CONTAINER_IMAGE",
            _ => throw new ArgumentException(
                "resourceType must be one of: ec2, lambda, ecr.",
                nameof(resourceType))
        };

    private IAmazonInspector2 GetRegionalClient(string region)
    {
        if (string.Equals(_inspectorClient.Config.RegionEndpoint?.SystemName, region, System.StringComparison.OrdinalIgnoreCase))
            return _inspectorClient;

        return new AmazonInspector2Client(RegionEndpoint.GetBySystemName(region));
    }
}
