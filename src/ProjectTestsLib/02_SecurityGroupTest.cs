using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;
using NUnit.Framework;
using ProjectTestsLib.Helper;
namespace ProjectTestsLib;

[GameClass(3), CancelAfter(Constants.Timeout), Order(3)]
public class SecurityGroupTest
{
    private SessionAWSCredentials? Credential { get; set; }
    private AmazonEC2Client? AcctEc2Client { get; set; }

    private SecurityGroup? AlbSecurityGroup { get; set; }
    private SecurityGroup? LambdaSecurityGroup { get; set; }
    private SecurityGroup? DatabaseSecurityGroup { get; set; }

    private VpcEndpoint? SqsInterfaceEndpoint { get; set; }
    private VpcEndpoint? SecretsManagerInterfaceEndpoint { get; set; }


    [SetUp]
    public void Setup()
    {
        var credentialHelper = new CredentialHelper();
        Credential = credentialHelper.GetCredential();
        AcctEc2Client = new AmazonEC2Client(Credential);
        AlbSecurityGroup = QueryHelper.GetSecurityGroupByName(AcctEc2Client, "ALB Security Group");
        LambdaSecurityGroup = QueryHelper.GetSecurityGroupByName(AcctEc2Client, "Web Lambda Security Group");
        DatabaseSecurityGroup = QueryHelper.GetSecurityGroupByName(AcctEc2Client, "Database Security Group");
        SqsInterfaceEndpoint = QueryHelper.GetEndPointByServiceName(AcctEc2Client, "com.amazonaws.us-east-1.sqs");
    }

    [TearDown]
    public void TearDown()
    {
        AcctEc2Client?.Dispose();
    }
}