using Amazon.CostExplorer;
using Amazon.CostExplorer.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aws.Models.Configuration;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;

namespace Aws.Repository.Implementations;

public class CostRepository : ICostRepository
{
    private readonly IAmazonCostExplorer _costExplorer;
    private readonly ILogger<CostRepository> _logger;
    private readonly CostOptions _options;

    public CostRepository(
        IAmazonCostExplorer costExplorer,
        IOptions<CostOptions> costOptions,
        ILogger<CostRepository> logger)
    {
        _costExplorer = costExplorer;
        _logger = logger;
        _options = costOptions.Value;
    }

    public async Task<CostByProjectResponseDto> GetCostsByProjectAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        DateOnly awsEndDate = endDate.AddDays(1);
        Dictionary<string, (decimal Amount, string Currency)> projectTotals =
            new(StringComparer.OrdinalIgnoreCase);

        string? nextPageToken = null;

        do
        {
            GetCostAndUsageRequest request = new()
            {
                TimePeriod = new DateInterval
                {
                    Start = startDate.ToString("yyyy-MM-dd"),
                    End = awsEndDate.ToString("yyyy-MM-dd")
                },
                Granularity = Granularity.DAILY,
                Metrics = [_options.Metric],
                GroupBy =
                [
                    new GroupDefinition
                    {
                        Type = GroupDefinitionType.TAG,
                        Key = _options.ProjectTagKey
                    }
                ],
                NextPageToken = nextPageToken
            };

            _logger.LogDebug(
                "Querying Cost Explorer from {StartDate} to {EndDate} (exclusive end {AwsEndDate}) grouped by tag {TagKey}",
                startDate,
                endDate,
                awsEndDate,
                _options.ProjectTagKey);

            GetCostAndUsageResponse response =
                await _costExplorer.GetCostAndUsageAsync(request, cancellationToken);

            foreach (ResultByTime period in response.ResultsByTime)
            {
                foreach (Group group in period.Groups)
                {
                    string project = ParseProjectName(group.Keys.FirstOrDefault() ?? string.Empty);

                    if (!group.Metrics.TryGetValue(_options.Metric, out MetricValue? metricValue))
                        continue;

                    if (!decimal.TryParse(metricValue.Amount, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out decimal amount))
                        continue;

                    string currency = string.IsNullOrWhiteSpace(metricValue.Unit) ? "USD" : metricValue.Unit;

                    if (projectTotals.TryGetValue(project, out (decimal Amount, string Currency) existing))
                        projectTotals[project] = (existing.Amount + amount, existing.Currency);
                    else
                        projectTotals[project] = (amount, currency);
                }
            }

            nextPageToken = response.NextPageToken;
        }
        while (!string.IsNullOrEmpty(nextPageToken));

        string defaultCurrency = projectTotals.Values.FirstOrDefault().Currency ?? "USD";

        List<ProjectCostDto> projects = projectTotals
            .Select(kvp => new ProjectCostDto
            {
                Project = kvp.Key,
                Amount = Math.Round(kvp.Value.Amount, 4),
                Currency = kvp.Value.Currency
            })
            .OrderByDescending(p => p.Amount)
            .ToList();

        return new CostByProjectResponseDto
        {
            StartDate = startDate.ToString("yyyy-MM-dd"),
            EndDate = endDate.ToString("yyyy-MM-dd"),
            TotalAmount = Math.Round(projects.Sum(p => p.Amount), 4),
            Currency = defaultCurrency,
            ProjectTagKey = _options.ProjectTagKey,
            Projects = projects,
            ScannedAt = DateTime.UtcNow
        };
    }

    private static string ParseProjectName(string groupKey)
    {
        int separatorIndex = groupKey.IndexOf('$');
        if (separatorIndex < 0)
            return string.IsNullOrWhiteSpace(groupKey) ? "(untagged)" : groupKey;

        string value = groupKey[(separatorIndex + 1)..];
        return string.IsNullOrWhiteSpace(value) ? "(untagged)" : value;
    }
}
