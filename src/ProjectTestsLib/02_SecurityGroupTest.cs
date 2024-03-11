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
    private VpcEndpoint? DynamodbGatewayEndpoint { get; set; }

    private VpcEndpoint? S3GatewayEndpoint { get; set; }


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
        SecretsManagerInterfaceEndpoint = QueryHelper.GetEndPointByServiceName(AcctEc2Client, "com.amazonaws.us-east-1.secretsmanager");
        DynamodbGatewayEndpoint = QueryHelper.GetEndPointByServiceName(AcctEc2Client, "com.amazonaws.us-east-1.dynamodb");
        S3GatewayEndpoint = QueryHelper.GetEndPointByServiceName(AcctEc2Client, "com.amazonaws.us-east-1.s3");
    }

    [TearDown]
    public void TearDown()
    {
        AcctEc2Client?.Dispose();
    }

    [GameTask("In 'Cloud Project VPC', create 3 Security Groups named 'ALB Security Group', 'Web Lambda Security Group', and 'Database Security Group'.", 2, 10)]
    [Test, Order(1)]
    public void Test01_3SecurityGroups()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AlbSecurityGroup, Is.Not.Null);
            Assert.That(LambdaSecurityGroup, Is.Not.Null);
            Assert.That(DatabaseSecurityGroup, Is.Not.Null);
        });
    }

    [GameTask("In 'Cloud Project VPC', create 'ALB Security Group' rules: 1. Ingress rule from anywhere for port 80. 2. Egress rule to 'Web Lambda Security Group'.", 2, 10)]
    [Test, Order(2)]
    public void Test02_AlbSecurityGroupRules()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AlbSecurityGroup!.IpPermissions.Count, Is.EqualTo(1));
            Assert.That(AlbSecurityGroup!.IpPermissions[0].IpProtocol.ToLower(), Is.EqualTo("tcp"));
            Assert.That(AlbSecurityGroup!.IpPermissions[0].FromPort, Is.EqualTo(80));
            Assert.That(AlbSecurityGroup!.IpPermissions[0].ToPort, Is.EqualTo(80));
            Assert.That(AlbSecurityGroup!.IpPermissions[0].Ipv4Ranges[0].CidrIp, Is.EqualTo("0.0.0.0/0"));
            Assert.That(AlbSecurityGroup!.IpPermissionsEgress.Count, Is.EqualTo(1));
            Assert.That(AlbSecurityGroup!.IpPermissionsEgress[0].UserIdGroupPairs[0].GroupId, Is.EqualTo(LambdaSecurityGroup!.GroupId));
        });
    }

    [GameTask("In 'Cloud Project VPC', create 'Web Lambda Security Group' rules: 1. Ingress rule from Web Lambda Security Group for all traffic. 2. Egress rule to 'Database Security Group' for port 3306. 3. Egress rule to all 4 VPC endpoints.", 2, 10)]
    [Test, Order(3)]
    public void Test03_WebLambdaSecurityGroupRules()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LambdaSecurityGroup!.IpPermissions, Has.Count.EqualTo(1));
            Assert.That(LambdaSecurityGroup!.IpPermissions[0].IpProtocol.ToLower(), Is.EqualTo("-1"));
            Assert.That(LambdaSecurityGroup!.IpPermissions[0].FromPort, Is.EqualTo(0));
            Assert.That(LambdaSecurityGroup!.IpPermissions[0].UserIdGroupPairs[0].GroupId, Is.EqualTo(AlbSecurityGroup!.GroupId));
        });

        var dbEgressRule = LambdaSecurityGroup!.IpPermissionsEgress.FirstOrDefault(x => x.IpProtocol.ToLower() == "tcp" && x.ToPort == 3306);
        Assert.That(dbEgressRule, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(SqsInterfaceEndpoint, Is.Not.Null);
            Assert.That(SecretsManagerInterfaceEndpoint, Is.Not.Null);
            Assert.That(DynamodbGatewayEndpoint, Is.Not.Null);
            Assert.That(S3GatewayEndpoint, Is.Not.Null);
        });


        var interfaceEndpointEgressRule = LambdaSecurityGroup!.IpPermissionsEgress.FirstOrDefault(x => x.FromPort == 443 && x.ToPort == 443);
        Assert.That(interfaceEndpointEgressRule, Is.Not.Null);
        Assert.That(interfaceEndpointEgressRule.UserIdGroupPairs, Has.Count.EqualTo(2));

        var interfaceEndpointSecurityGroupIds = interfaceEndpointEgressRule.UserIdGroupPairs.Select(x => x.GroupId).ToArray();
        var expectedInterfaceEndpointSecurityGroupIds = new[] { SqsInterfaceEndpoint.Groups[0].GroupId, SecretsManagerInterfaceEndpoint.Groups[0].GroupId };
        Assert.That(interfaceEndpointSecurityGroupIds, Is.EquivalentTo(expectedInterfaceEndpointSecurityGroupIds));

        var gatewayEndpointEgressRule = LambdaSecurityGroup!.IpPermissionsEgress.FirstOrDefault(x => x.FromPort == 0 && x.ToPort == 65535);
        Assert.That(gatewayEndpointEgressRule, Is.Not.Null);
        Assert.That(gatewayEndpointEgressRule.PrefixListIds, Has.Count.EqualTo(2));
        var gatewayEndpointPrefixListIds = gatewayEndpointEgressRule.PrefixListIds.Select(x => x.Id).ToArray();
        Console.WriteLine(string.Join(", ", gatewayEndpointPrefixListIds));

        DescribePrefixListsRequest describePrefixListsRequest = new()
        {
            Filters = [
                new() { Name = "prefix-list-name", Values = ["com.amazonaws.us-east-1.s3","com.amazonaws.us-east-1.dynamodb"] },
            ]
        };
        var describePrefixListsResponse = AcctEc2Client!.DescribePrefixListsAsync(describePrefixListsRequest).Result;
        var expectedPrefixListIds = describePrefixListsResponse.PrefixLists.Select(x => x.PrefixListId).ToArray();
        Assert.That(gatewayEndpointPrefixListIds, Is.EquivalentTo(expectedPrefixListIds));
    }

    [GameTask("In 'Cloud Project VPC', create 'Database Security Group' rules: 1. Ingress rule from anywhere for port 80. 2. Egress rule to 'Web Lambda Security Group'.", 2, 10)]
    [Test, Order(4)]
    public void Test04_DatabaseSecurityGroupRules()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DatabaseSecurityGroup!.IpPermissions.Count, Is.EqualTo(1));
            Assert.That(DatabaseSecurityGroup!.IpPermissions[0].IpProtocol.ToLower(), Is.EqualTo("tcp"));
            Assert.That(DatabaseSecurityGroup!.IpPermissions[0].FromPort, Is.EqualTo(3306));
            Assert.That(DatabaseSecurityGroup!.IpPermissions[0].ToPort, Is.EqualTo(3306));
            Assert.That(DatabaseSecurityGroup!.IpPermissions[0].UserIdGroupPairs[0].GroupId, Is.EqualTo(LambdaSecurityGroup!.GroupId));
            Assert.That(DatabaseSecurityGroup!.IpPermissionsEgress, Has.Count.EqualTo(1));;
        });
    }
}