namespace Aws.Models.Configuration;

public class Ec2Options
{
    public const string SectionName = "Ec2";
    public List<string> PublicInboundCidrs { get; set; } = ["0.0.0.0/0", "::/0"];
}
