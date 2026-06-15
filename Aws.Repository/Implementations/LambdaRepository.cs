using System.Net;
using System.Text.Json;
using Amazon;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;

namespace Aws.Repository.Implementations;

public class LambdaRepository : ILambdaRepository
{
    private readonly IAmazonLambda _lambdaClient;
    private readonly ILogger<LambdaRepository> _logger;

    public LambdaRepository(IAmazonLambda lambdaClient, ILogger<LambdaRepository> logger)
    {
        _lambdaClient = lambdaClient;
        _logger = logger;
    }

    public async Task<LambdaPublicFunctionsResponseDto> GetPublicFunctionsByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        string normalizedRegion = NormalizeRegion(region);
        IAmazonLambda client = GetRegionalClient(normalizedRegion);

        List<FunctionConfiguration> functions = await ListFunctionsAsync(client, cancellationToken);
        List<LambdaPublicFunctionDto> publicFunctions = new();

        foreach (FunctionConfiguration function in functions)
        {
            if (string.IsNullOrWhiteSpace(function.FunctionName) || string.IsNullOrWhiteSpace(function.FunctionArn))
                continue;

            (bool IsPublic, List<string> Reasons, string? FunctionUrl) analysis =
                await AnalyzePublicAccessAsync(client, function.FunctionName, cancellationToken);

            if (!analysis.IsPublic)
                continue;

            publicFunctions.Add(new LambdaPublicFunctionDto
            {
                FunctionName = function.FunctionName,
                FunctionArn = function.FunctionArn,
                Runtime = function.Runtime,
                State = function.State?.Value,
                FunctionUrl = analysis.FunctionUrl,
                PublicAccessReasons = analysis.Reasons,
                Recommendation = BuildRecommendation(analysis.Reasons)
            });
        }

        _logger.LogInformation(
            "Found {PublicCount}/{TotalCount} publicly accessible Lambda functions in region {Region}",
            publicFunctions.Count,
            functions.Count,
            normalizedRegion);

        return new LambdaPublicFunctionsResponseDto
        {
            Region = normalizedRegion,
            TotalFunctions = functions.Count,
            PublicFunctionsCount = publicFunctions.Count,
            Functions = publicFunctions
                .OrderBy(function => function.FunctionName, StringComparer.Ordinal)
                .ToList(),
            ScannedAt = DateTime.UtcNow
        };
    }

    private static async Task<List<FunctionConfiguration>> ListFunctionsAsync(
        IAmazonLambda client,
        CancellationToken cancellationToken)
    {
        List<FunctionConfiguration> functions = new();
        string? marker = null;

        do
        {
            ListFunctionsResponse response = await client.ListFunctionsAsync(
                new ListFunctionsRequest { Marker = marker },
                cancellationToken);

            if (response.Functions is not null)
                functions.AddRange(response.Functions);

            marker = response.NextMarker;
        }
        while (!string.IsNullOrEmpty(marker));

        return functions;
    }

    private static async Task<(bool IsPublic, List<string> Reasons, string? FunctionUrl)> AnalyzePublicAccessAsync(
        IAmazonLambda client,
        string functionName,
        CancellationToken cancellationToken)
    {
        List<string> reasons = new();
        string? functionUrl = null;

        if (await HasPublicFunctionUrlAsync(client, functionName, cancellationToken) is (true, string url))
        {
            reasons.Add("FunctionUrlNoAuth");
            functionUrl = url;
        }

        if (await HasPublicResourcePolicyAsync(client, functionName, cancellationToken))
            reasons.Add("PublicResourcePolicy");

        return (reasons.Count > 0, reasons, functionUrl);
    }

    private static async Task<(bool IsPublic, string? FunctionUrl)> HasPublicFunctionUrlAsync(
        IAmazonLambda client,
        string functionName,
        CancellationToken cancellationToken)
    {
        try
        {
            GetFunctionUrlConfigResponse response = await client.GetFunctionUrlConfigAsync(
                new GetFunctionUrlConfigRequest { FunctionName = functionName },
                cancellationToken);

            bool isPublic = string.Equals(response.AuthType?.Value, "NONE", StringComparison.OrdinalIgnoreCase);

            return (isPublic, response.FunctionUrl);
        }
        catch (AmazonLambdaException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return (false, null);
        }
    }

    private static async Task<bool> HasPublicResourcePolicyAsync(
        IAmazonLambda client,
        string functionName,
        CancellationToken cancellationToken)
    {
        try
        {
            GetPolicyResponse response = await client.GetPolicyAsync(
                new GetPolicyRequest { FunctionName = functionName },
                cancellationToken);

            return IsPolicyPubliclyInvocable(response.Policy);
        }
        catch (AmazonLambdaException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    internal static bool IsPolicyPubliclyInvocable(string? policyJson)
    {
        if (string.IsNullOrWhiteSpace(policyJson))
            return false;

        try
        {
            using JsonDocument document = JsonDocument.Parse(policyJson);
            if (!document.RootElement.TryGetProperty("Statement", out JsonElement statements))
                return false;

            foreach (JsonElement statement in statements.EnumerateArray())
            {
                if (!IsAllowStatement(statement))
                    continue;

                if (HasPublicPrincipal(statement))
                    return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool IsAllowStatement(JsonElement statement) =>
        statement.TryGetProperty("Effect", out JsonElement effect) &&
        string.Equals(effect.GetString(), "Allow", StringComparison.OrdinalIgnoreCase);

    private static bool HasPublicPrincipal(JsonElement statement)
    {
        if (!statement.TryGetProperty("Principal", out JsonElement principal))
            return false;

        if (principal.ValueKind == JsonValueKind.String)
            return string.Equals(principal.GetString(), "*", StringComparison.Ordinal);

        if (principal.TryGetProperty("AWS", out JsonElement awsPrincipal))
        {
            if (awsPrincipal.ValueKind == JsonValueKind.String)
                return string.Equals(awsPrincipal.GetString(), "*", StringComparison.Ordinal);

            foreach (JsonElement entry in awsPrincipal.EnumerateArray())
            {
                if (string.Equals(entry.GetString(), "*", StringComparison.Ordinal))
                    return true;
            }
        }

        return principal.TryGetProperty("Service", out JsonElement servicePrincipal) &&
               servicePrincipal.ValueKind == JsonValueKind.String &&
               string.Equals(servicePrincipal.GetString(), "*", StringComparison.Ordinal);
    }

    private IAmazonLambda GetRegionalClient(string region)
    {
        if (string.Equals(_lambdaClient.Config.RegionEndpoint?.SystemName, region, StringComparison.OrdinalIgnoreCase))
            return _lambdaClient;

        return new AmazonLambdaClient(RegionEndpoint.GetBySystemName(region));
    }

    private static string NormalizeRegion(string region) =>
        string.IsNullOrWhiteSpace(region) ? "us-east-1" : region;

    private static string BuildRecommendation(IReadOnlyList<string> reasons)
    {
        if (reasons.Contains("FunctionUrlNoAuth") && reasons.Contains("PublicResourcePolicy"))
            return "Disable or protect the function URL with AWS_IAM auth and remove public invoke permissions from the resource policy.";

        if (reasons.Contains("FunctionUrlNoAuth"))
            return "Remove the function URL or change AuthType from NONE to AWS_IAM.";

        return "Review the Lambda resource policy and remove public invoke permissions.";
    }
}
