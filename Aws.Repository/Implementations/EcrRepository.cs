using System.Net;
using Amazon;
using Amazon.ECR;
using Amazon.ECR.Model;
using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;

namespace Aws.Repository.Implementations;

public class EcrRepository : IEcrRepository
{
    private readonly IAmazonECR _ecrClient;
    private readonly ILogger<EcrRepository> _logger;

    public EcrRepository(IAmazonECR ecrClient, ILogger<EcrRepository> logger)
    {
        _ecrClient = ecrClient;
        _logger = logger;
    }

    public async Task<EcrRepositoryRisksResponseDto> GetRepositoryRisksByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        string normalizedRegion = NormalizeRegion(region);
        IAmazonECR client = GetRegionalClient(normalizedRegion);
        List<Amazon.ECR.Model.Repository> repositories = await ListRepositoriesAsync(client, cancellationToken);
        List<EcrRepositoryRiskDto> repositoriesAtRisk = new();

        foreach (Amazon.ECR.Model.Repository repository in repositories)
        {
            if (string.IsNullOrWhiteSpace(repository.RepositoryName) ||
                string.IsNullOrWhiteSpace(repository.RepositoryArn))
            {
                continue;
            }

            bool scanOnPushEnabled = repository.ImageScanningConfiguration?.ScanOnPush == true;
            bool isPublic = await IsRepositoryPublicAsync(client, repository.RepositoryName, cancellationToken);

            List<string> riskReasons = new();
            if (isPublic)
                riskReasons.Add("PublicRepositoryPolicy");

            if (!scanOnPushEnabled)
                riskReasons.Add("ScanOnPushDisabled");

            if (riskReasons.Count == 0)
                continue;

            repositoriesAtRisk.Add(new EcrRepositoryRiskDto
            {
                RepositoryName = repository.RepositoryName,
                RepositoryArn = repository.RepositoryArn,
                IsPublic = isPublic,
                ScanOnPushEnabled = scanOnPushEnabled,
                RiskReasons = riskReasons,
                Recommendation = BuildRecommendation(riskReasons)
            });
        }

        List<EcrRepositoryRiskDto> ordered = repositoriesAtRisk
            .OrderBy(repository => repository.RepositoryName, StringComparer.Ordinal)
            .ToList();

        _logger.LogInformation(
            "Found {RiskCount}/{TotalCount} ECR repositories with security risks in region {Region}",
            ordered.Count,
            repositories.Count,
            normalizedRegion);

        return new EcrRepositoryRisksResponseDto
        {
            Region = normalizedRegion,
            TotalRepositoriesScanned = repositories.Count,
            RepositoriesAtRiskCount = ordered.Count,
            Repositories = ordered,
            ScannedAt = DateTime.UtcNow
        };
    }

    private static async Task<List<Amazon.ECR.Model.Repository>> ListRepositoriesAsync(
        IAmazonECR client,
        CancellationToken cancellationToken)
    {
        List<Amazon.ECR.Model.Repository> repositories = new();
        string? nextToken = null;

        do
        {
            DescribeRepositoriesResponse response = await client.DescribeRepositoriesAsync(
                new DescribeRepositoriesRequest { NextToken = nextToken },
                cancellationToken);

            if (response.Repositories is not null)
                repositories.AddRange(response.Repositories);

            nextToken = response.NextToken;
        }
        while (!string.IsNullOrEmpty(nextToken));

        return repositories;
    }

    private static async Task<bool> IsRepositoryPublicAsync(
        IAmazonECR client,
        string repositoryName,
        CancellationToken cancellationToken)
    {
        try
        {
            GetRepositoryPolicyResponse response = await client.GetRepositoryPolicyAsync(
                new GetRepositoryPolicyRequest { RepositoryName = repositoryName },
                cancellationToken);

            return LambdaRepository.IsPolicyPubliclyInvocable(response.PolicyText);
        }
        catch (AmazonECRException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private static string BuildRecommendation(IReadOnlyList<string> riskReasons)
    {
        if (riskReasons.Contains("PublicRepositoryPolicy") && riskReasons.Contains("ScanOnPushDisabled"))
        {
            return "Remove public pull permissions from the repository policy and enable scan-on-push for image vulnerability detection.";
        }

        if (riskReasons.Contains("PublicRepositoryPolicy"))
            return "Remove public pull permissions from the repository policy or restrict principals to trusted AWS accounts.";

        return "Enable image scan-on-push to detect vulnerabilities before deployment.";
    }

    private IAmazonECR GetRegionalClient(string region)
    {
        if (string.Equals(_ecrClient.Config.RegionEndpoint?.SystemName, region, StringComparison.OrdinalIgnoreCase))
            return _ecrClient;

        return new AmazonECRClient(RegionEndpoint.GetBySystemName(region));
    }

    private static string NormalizeRegion(string region) =>
        string.IsNullOrWhiteSpace(region) ? "us-east-1" : region;
}
