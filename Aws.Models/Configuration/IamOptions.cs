namespace Aws.Models.Configuration;

public class IamOptions
{
    public const string SectionName = "Iam";
    public int AccessKeyRotationMaxAgeDays { get; set; } = 90;
}
