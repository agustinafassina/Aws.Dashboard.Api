namespace Aws.Models.Configuration;

public class S3Options
{
    public const string SectionName = "S3";
    public string AllUsersUri { get; set; } = "http://acs.amazonaws.com/groups/global/AllUsers";
    public string AuthenticatedUsersUri { get; set; } = "http://acs.amazonaws.com/groups/global/AuthenticatedUsers";
}
