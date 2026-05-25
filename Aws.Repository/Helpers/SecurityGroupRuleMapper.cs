using Amazon.EC2.Model;
using Aws.Models.Dto;

namespace Aws.Repository.Helpers;

public static class SecurityGroupRuleMapper
{
    public static SecurityGroupDto MapSecurityGroup(
        string groupId,
        string? groupName,
        SecurityGroup? securityGroup,
        IReadOnlySet<string> publicInboundCidrs)
    {
        if (securityGroup == null)
        {
            return new SecurityGroupDto
            {
                GroupId = groupId,
                GroupName = groupName ?? groupId,
                Description = null,
                InboundRules = []
            };
        }

        return new SecurityGroupDto
        {
            GroupId = securityGroup.GroupId,
            GroupName = securityGroup.GroupName,
            Description = securityGroup.Description,
            InboundRules = MapInboundRules(securityGroup.IpPermissions, publicInboundCidrs)
        };
    }

    public static List<InboundRuleDto> MapInboundRules(
        IEnumerable<IpPermission> permissions,
        IReadOnlySet<string> publicInboundCidrs)
    {
        var rules = new List<InboundRuleDto>();

        foreach (var permission in permissions)
        {
            string? protocol = permission.IpProtocol ?? "unknown";
            string? portRange = FormatPortRange(protocol, permission.FromPort, permission.ToPort);

            foreach (var range in permission.Ipv4Ranges)
            {
                rules.Add(CreateRule(
                    protocol,
                    permission.FromPort,
                    permission.ToPort,
                    portRange,
                    range.CidrIp,
                    "Cidr",
                    range.Description,
                    publicInboundCidrs));
            }

            foreach (var range in permission.Ipv6Ranges)
            {
                rules.Add(CreateRule(
                    protocol,
                    permission.FromPort,
                    permission.ToPort,
                    portRange,
                    range.CidrIpv6,
                    "Cidr",
                    range.Description,
                    publicInboundCidrs));
            }

            foreach (var pair in permission.UserIdGroupPairs)
            {
                string? source = !string.IsNullOrWhiteSpace(pair.GroupId)
                    ? pair.GroupId
                    : pair.GroupName ?? "unknown";

                rules.Add(CreateRule(
                    protocol,
                    permission.FromPort,
                    permission.ToPort,
                    portRange,
                    source,
                    "SecurityGroup",
                    pair.Description,
                    publicInboundCidrs));
            }

            foreach (var prefixList in permission.PrefixListIds)
            {
                rules.Add(CreateRule(
                    protocol,
                    permission.FromPort,
                    permission.ToPort,
                    portRange,
                    prefixList.Id ?? "unknown",
                    "PrefixList",
                    prefixList.Description,
                    publicInboundCidrs));
            }
        }

        return rules;
    }

    public static string BuildEc2Recommendation(IReadOnlyList<InboundRuleDto> publicRules)
    {
        if (publicRules.Count == 0)
            return "No inbound rules exposed to the public internet detected on attached security groups.";

        if (publicRules.Any(IsSshRule))
            return "Restrict SSH (port 22) access to trusted IP ranges instead of 0.0.0.0/0 or ::/0.";

        if (publicRules.Any(IsRdpRule))
            return "Restrict RDP (port 3389) access to trusted IP ranges instead of 0.0.0.0/0 or ::/0.";

        return "Review inbound rules and restrict public access (0.0.0.0/0 or ::/0) to required ports and trusted sources only.";
    }

    public static string BuildRdsRecommendation(
        bool publiclyAccessible,
        string? engine,
        int? dbPort,
        IReadOnlyList<InboundRuleDto> publicRules)
    {
        if (publicRules.Count == 0 && !publiclyAccessible)
            return "No inbound rules exposed to the public internet detected on attached security groups.";

        if (publicRules.Count == 0 && publiclyAccessible)
            return "RDS instance is publicly accessible. Disable public access unless strictly required and protect with security groups.";

        if (publiclyAccessible && publicRules.Any(r => CoversPort(r, dbPort ?? -1)))
            return $"RDS is publicly accessible and database port {dbPort} is open to the internet. Disable public access and restrict port {dbPort} to trusted sources only.";

        if (publicRules.Any(r => CoversPort(r, dbPort ?? -1)))
            return $"Database port {dbPort} ({engine}) is open to the public internet. Restrict access to application tier security groups or trusted IP ranges only.";

        if (publiclyAccessible)
            return "RDS instance is publicly accessible. Disable public access unless strictly required.";

        return "Review inbound rules and restrict public access (0.0.0.0/0 or ::/0) to required ports and trusted sources only.";
    }

    private static InboundRuleDto CreateRule(
        string protocol,
        int? fromPort,
        int? toPort,
        string portRange,
        string? source,
        string sourceType,
        string? description,
        IReadOnlySet<string> publicInboundCidrs)
    {
        string? normalizedSource = source ?? "unknown";
        bool isPublic = sourceType == "Cidr" && publicInboundCidrs.Contains(normalizedSource);

        return new InboundRuleDto
        {
            Protocol = protocol,
            FromPort = fromPort,
            ToPort = toPort,
            PortRange = portRange,
            Source = normalizedSource,
            SourceType = sourceType,
            IsPubliclyExposed = isPublic,
            Description = description
        };
    }

    private static string FormatPortRange(string protocol, int? fromPort, int? toPort)
    {
        if (protocol == "-1")
            return "All";

        if (!fromPort.HasValue && !toPort.HasValue)
            return "All";

        if (fromPort == toPort)
            return fromPort?.ToString() ?? "All";

        return $"{fromPort}-{toPort}";
    }

    private static bool IsSshRule(InboundRuleDto rule) =>
        CoversPort(rule, 22) && IsTcpOrAll(rule.Protocol);

    private static bool IsRdpRule(InboundRuleDto rule) =>
        CoversPort(rule, 3389) && IsTcpOrAll(rule.Protocol);

    private static bool CoversPort(InboundRuleDto rule, int port)
    {
        if (port < 0)
            return false;

        if (rule.Protocol == "-1")
            return true;

        if (!rule.FromPort.HasValue && !rule.ToPort.HasValue)
            return true;

        int? from = rule.FromPort ?? rule.ToPort ?? port;
        int? to = rule.ToPort ?? rule.FromPort ?? port;
        return from <= port && to >= port;
    }

    private static bool IsTcpOrAll(string protocol) =>
        protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase) || protocol == "-1";
}
