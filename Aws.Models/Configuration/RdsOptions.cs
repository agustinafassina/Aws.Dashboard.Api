namespace Aws.Models.Configuration;

public class RdsOptions
{
    public const string SectionName = "Rds";
    public List<string> PublicInboundCidrs { get; set; } = ["0.0.0.0/0", "::/0"];
    public int MinBackupRetentionDays { get; set; } = 7;
}
