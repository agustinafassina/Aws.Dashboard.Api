using Amazon.Inspector2.Model;
using Aws.Models.Dto;

namespace Aws.Repository.Helpers;

internal static class InspectorFindingMapper
{
    public static InspectorFindingDto MapFinding(Finding finding)
    {
        Resource? resource = finding.Resources?.FirstOrDefault();

        return new InspectorFindingDto
        {
            FindingArn = finding.FindingArn ?? string.Empty,
            Title = finding.Title ?? string.Empty,
            Description = finding.Description,
            Severity = finding.Severity?.Value ?? "UNKNOWN",
            Status = finding.Status?.Value ?? "UNKNOWN",
            Type = finding.Type?.Value ?? "UNKNOWN",
            ResourceType = resource?.Type,
            ResourceId = resource?.Id,
            VulnerabilityId = finding.PackageVulnerabilityDetails?.VulnerabilityId
                ?? finding.CodeVulnerabilityDetails?.Cwes?.FirstOrDefault()
                ?? finding.CodeVulnerabilityDetails?.RuleId,
            FixAvailable = finding.FixAvailable?.Value,
            ExploitAvailable = finding.ExploitAvailable?.Value,
            FirstObservedAt = ToUtcDateTime(finding.FirstObservedAt),
            LastObservedAt = ToUtcDateTime(finding.LastObservedAt),
            UpdatedAt = ToUtcDateTime(finding.UpdatedAt),
            Recommendation = finding.Remediation?.Recommendation?.Text,
            Resource = MapResource(resource)
        };
    }

    private static InspectorFindingResourceDto? MapResource(Resource? resource)
    {
        if (resource?.Details is null)
            return null;

        if (resource.Details.AwsEc2Instance is not null)
        {
            AwsEc2InstanceDetails ec2 = resource.Details.AwsEc2Instance;
            return new InspectorFindingResourceDto
            {
                InstanceId = resource.Id,
                VpcId = ec2.VpcId,
                SubnetId = ec2.SubnetId,
                ImageId = ec2.ImageId
            };
        }

        if (resource.Details.AwsLambdaFunction is not null)
        {
            AwsLambdaFunctionDetails lambda = resource.Details.AwsLambdaFunction;
            return new InspectorFindingResourceDto
            {
                FunctionName = lambda.FunctionName ?? resource.Id,
                Runtime = lambda.Runtime
            };
        }

        if (resource.Details.AwsEcrContainerImage is not null)
        {
            AwsEcrContainerImageDetails ecr = resource.Details.AwsEcrContainerImage;
            return new InspectorFindingResourceDto
            {
                RepositoryName = ecr.RepositoryName,
                ImageHash = ecr.ImageHash ?? resource.Id,
                ImageTags = ecr.ImageTags ?? []
            };
        }

        return new InspectorFindingResourceDto
        {
            InstanceId = resource.Id
        };
    }

    private static DateTime? ToUtcDateTime(DateTime? value) =>
        value?.ToUniversalTime();
}
