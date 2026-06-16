using Amazon;
using Amazon.ElasticLoadBalancing;
using Amazon.ElasticLoadBalancingV2;
using ClassicElb = Amazon.ElasticLoadBalancing.Model;
using ElbV2 = Amazon.ElasticLoadBalancingV2.Model;
using Microsoft.Extensions.Logging;
using Aws.Models.Dto;
using Aws.Repository.Interfaces;

namespace Aws.Repository.Implementations;

public class ElbRepository : IElbRepository
{
    private readonly IAmazonElasticLoadBalancingV2 _elbV2Client;
    private readonly IAmazonElasticLoadBalancing _classicElbClient;
    private readonly ILogger<ElbRepository> _logger;

    public ElbRepository(
        IAmazonElasticLoadBalancingV2 elbV2Client,
        IAmazonElasticLoadBalancing classicElbClient,
        ILogger<ElbRepository> logger)
    {
        _elbV2Client = elbV2Client;
        _classicElbClient = classicElbClient;
        _logger = logger;
    }

    public async Task<ElbPublicListenersResponseDto> GetPublicListenersByRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        string normalizedRegion = NormalizeRegion(region);
        IAmazonElasticLoadBalancingV2 elbV2 = GetRegionalElbV2Client(normalizedRegion);
        IAmazonElasticLoadBalancing classicElb = GetRegionalClassicElbClient(normalizedRegion);

        List<ElbV2.LoadBalancer> v2LoadBalancers = await ListV2LoadBalancersAsync(elbV2, cancellationToken);
        List<ClassicElb.LoadBalancerDescription> classicLoadBalancers =
            await ListClassicLoadBalancersAsync(classicElb, cancellationToken);

        List<ElbPublicListenerDto> publicLoadBalancers = new();
        int totalScanned = v2LoadBalancers.Count + classicLoadBalancers.Count;

        foreach (ElbV2.LoadBalancer loadBalancer in v2LoadBalancers)
        {
            if (string.IsNullOrWhiteSpace(loadBalancer.LoadBalancerArn) ||
                string.IsNullOrWhiteSpace(loadBalancer.LoadBalancerName))
            {
                continue;
            }

            if (!IsInternetFacing(loadBalancer))
                continue;

            List<ElbV2.Listener> listeners = await ListListenersAsync(elbV2, loadBalancer.LoadBalancerArn, cancellationToken);
            List<ElbListenerDetailDto> listenerDetails = listeners
                .Select(listener => new ElbListenerDetailDto
                {
                    Port = listener.Port.GetValueOrDefault(),
                    Protocol = listener.Protocol?.Value ?? "unknown"
                })
                .OrderBy(listener => listener.Port)
                .ToList();

            List<string> riskReasons = BuildV2RiskReasons(loadBalancer, listenerDetails);
            if (riskReasons.Count == 0)
                continue;

            publicLoadBalancers.Add(new ElbPublicListenerDto
            {
                LoadBalancerName = loadBalancer.LoadBalancerName,
                LoadBalancerArn = loadBalancer.LoadBalancerArn,
                LoadBalancerType = loadBalancer.Type?.Value ?? "unknown",
                Scheme = loadBalancer.Scheme?.Value ?? "unknown",
                Listeners = listenerDetails,
                RiskReasons = riskReasons,
                Recommendation = BuildRecommendation(riskReasons, loadBalancer.Type?.Value)
            });
        }

        foreach (ClassicElb.LoadBalancerDescription loadBalancer in classicLoadBalancers)
        {
            if (string.IsNullOrWhiteSpace(loadBalancer.LoadBalancerName))
                continue;

            if (!string.Equals(loadBalancer.Scheme, "internet-facing", StringComparison.OrdinalIgnoreCase))
                continue;

            List<ElbListenerDetailDto> listenerDetails = (loadBalancer.ListenerDescriptions ?? [])
                .Select(description => new ElbListenerDetailDto
                {
                    Port = description.Listener?.LoadBalancerPort ?? 0,
                    Protocol = description.Listener?.Protocol ?? "unknown"
                })
                .OrderBy(listener => listener.Port)
                .ToList();

            List<string> riskReasons = BuildClassicRiskReasons(listenerDetails);
            if (riskReasons.Count == 0)
                continue;

            publicLoadBalancers.Add(new ElbPublicListenerDto
            {
                LoadBalancerName = loadBalancer.LoadBalancerName,
                LoadBalancerArn = BuildClassicArn(loadBalancer, normalizedRegion),
                LoadBalancerType = "classic",
                Scheme = loadBalancer.Scheme ?? "internet-facing",
                Listeners = listenerDetails,
                RiskReasons = riskReasons,
                Recommendation = BuildRecommendation(riskReasons, "classic")
            });
        }

        List<ElbPublicListenerDto> ordered = publicLoadBalancers
            .OrderBy(loadBalancer => loadBalancer.LoadBalancerName, StringComparer.Ordinal)
            .ToList();

        _logger.LogInformation(
            "Found {PublicCount}/{TotalCount} internet-facing load balancers with public listener risks in region {Region}",
            ordered.Count,
            totalScanned,
            normalizedRegion);

        return new ElbPublicListenersResponseDto
        {
            Region = normalizedRegion,
            TotalLoadBalancersScanned = totalScanned,
            PublicLoadBalancersCount = ordered.Count,
            LoadBalancers = ordered,
            ScannedAt = DateTime.UtcNow
        };
    }

    private static bool IsInternetFacing(ElbV2.LoadBalancer loadBalancer) =>
        string.Equals(loadBalancer.Scheme?.Value, "internet-facing", StringComparison.OrdinalIgnoreCase);

    private static List<string> BuildV2RiskReasons(
        ElbV2.LoadBalancer loadBalancer,
        IReadOnlyList<ElbListenerDetailDto> listeners)
    {
        List<string> reasons = ["InternetFacing"];

        if (listeners.Any(listener => IsUnencryptedHttp(listener.Protocol)))
            reasons.Add("UnencryptedHttpListener");

        if (string.Equals(loadBalancer.Type?.Value, "network", StringComparison.OrdinalIgnoreCase))
            reasons.Add("PublicNetworkLoadBalancer");

        return reasons;
    }

    private static List<string> BuildClassicRiskReasons(IReadOnlyList<ElbListenerDetailDto> listeners)
    {
        List<string> reasons = ["InternetFacing"];

        if (listeners.Any(listener => IsUnencryptedHttp(listener.Protocol)))
            reasons.Add("UnencryptedHttpListener");

        return reasons;
    }

    private static bool IsUnencryptedHttp(string? protocol) =>
        string.Equals(protocol, "HTTP", StringComparison.OrdinalIgnoreCase);

    private static string BuildRecommendation(IReadOnlyList<string> riskReasons, string? loadBalancerType)
    {
        if (riskReasons.Contains("UnencryptedHttpListener"))
        {
            return "Use HTTPS listeners or redirect HTTP to HTTPS. Restrict internet-facing exposure where possible.";
        }

        if (string.Equals(loadBalancerType, "network", StringComparison.OrdinalIgnoreCase))
        {
            return "Review whether this network load balancer must be internet-facing. Restrict security groups and target exposure.";
        }

        return "Review internet-facing load balancer exposure and enforce TLS on all public listeners.";
    }

    private static async Task<List<ElbV2.LoadBalancer>> ListV2LoadBalancersAsync(
        IAmazonElasticLoadBalancingV2 client,
        CancellationToken cancellationToken)
    {
        List<ElbV2.LoadBalancer> loadBalancers = new();
        string? marker = null;

        do
        {
            ElbV2.DescribeLoadBalancersResponse response = await client.DescribeLoadBalancersAsync(
                new ElbV2.DescribeLoadBalancersRequest { Marker = marker },
                cancellationToken);

            if (response.LoadBalancers is not null)
                loadBalancers.AddRange(response.LoadBalancers);

            marker = response.NextMarker;
        }
        while (!string.IsNullOrEmpty(marker));

        return loadBalancers;
    }

    private static async Task<List<ElbV2.Listener>> ListListenersAsync(
        IAmazonElasticLoadBalancingV2 client,
        string loadBalancerArn,
        CancellationToken cancellationToken)
    {
        List<ElbV2.Listener> listeners = new();
        string? marker = null;

        do
        {
            ElbV2.DescribeListenersResponse response = await client.DescribeListenersAsync(
                new ElbV2.DescribeListenersRequest
                {
                    LoadBalancerArn = loadBalancerArn,
                    Marker = marker
                },
                cancellationToken);

            if (response.Listeners is not null)
                listeners.AddRange(response.Listeners);

            marker = response.NextMarker;
        }
        while (!string.IsNullOrEmpty(marker));

        return listeners;
    }

    private static async Task<List<ClassicElb.LoadBalancerDescription>> ListClassicLoadBalancersAsync(
        IAmazonElasticLoadBalancing client,
        CancellationToken cancellationToken)
    {
        List<ClassicElb.LoadBalancerDescription> loadBalancers = new();
        string? marker = null;

        do
        {
            ClassicElb.DescribeLoadBalancersResponse response = await client.DescribeLoadBalancersAsync(
                new ClassicElb.DescribeLoadBalancersRequest { Marker = marker },
                cancellationToken);

            if (response.LoadBalancerDescriptions is not null)
                loadBalancers.AddRange(response.LoadBalancerDescriptions);

            marker = response.NextMarker;
        }
        while (!string.IsNullOrEmpty(marker));

        return loadBalancers;
    }

    private static string BuildClassicArn(ClassicElb.LoadBalancerDescription loadBalancer, string region) =>
        $"arn:aws:elasticloadbalancing:{region}:classic:loadbalancer/{loadBalancer.LoadBalancerName}";

    private IAmazonElasticLoadBalancingV2 GetRegionalElbV2Client(string region)
    {
        if (string.Equals(_elbV2Client.Config.RegionEndpoint?.SystemName, region, StringComparison.OrdinalIgnoreCase))
            return _elbV2Client;

        return new AmazonElasticLoadBalancingV2Client(RegionEndpoint.GetBySystemName(region));
    }

    private IAmazonElasticLoadBalancing GetRegionalClassicElbClient(string region)
    {
        if (string.Equals(_classicElbClient.Config.RegionEndpoint?.SystemName, region, StringComparison.OrdinalIgnoreCase))
            return _classicElbClient;

        return new AmazonElasticLoadBalancingClient(RegionEndpoint.GetBySystemName(region));
    }

    private static string NormalizeRegion(string region) =>
        string.IsNullOrWhiteSpace(region) ? "us-east-1" : region;
}
