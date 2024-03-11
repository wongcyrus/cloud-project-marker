using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;
using NUnit.Framework;
using ProjectTestsLib.Helper;
namespace ProjectTestsLib;

[GameClass(2), CancelAfter(Constants.Timeout), Order(2)]
public class VpcTest
{
    private SessionAWSCredentials? Credential { get; set; }
    private AmazonEC2Client? AcctEc2Client { get; set; }
    private string? VpcId { get; set; }

    [SetUp]
    public void Setup()
    {
        var credentialHelper = new CredentialHelper();
        Credential = credentialHelper.GetCredential();
        AcctEc2Client = new AmazonEC2Client(Credential);

        var describeVpcsRequest = new DescribeVpcsRequest();
        describeVpcsRequest.Filters.Add(new Filter("tag:Name", ["Cloud Project VPC"]));
        var describeVpcsResponse = AcctEc2Client!.DescribeVpcsAsync(describeVpcsRequest).Result;
        VpcId = describeVpcsResponse.Vpcs[0].VpcId;
    }

    [TearDown]
    public void TearDown()
    {
        AcctEc2Client?.Dispose();
    }

    [GameTask("Can you create a VPC with CIDR 10.0.0.0/16 and name it as 'Cloud Project VPC'?", 2, 10, groupNumber: 1)]
    [Test, Order(1)]
    public async Task Test01_VpcExist()
    {
        var describeVpcsRequest = new DescribeVpcsRequest();
        describeVpcsRequest.Filters.Add(new Filter("tag:Name", ["Cloud Project VPC"]));
        var describeVpcsResponse = await AcctEc2Client!.DescribeVpcsAsync(describeVpcsRequest);

        Assert.That(describeVpcsResponse.Vpcs, Has.Count.EqualTo(1));
    }

    [GameTask("In 'Cloud Project VPC', Can you create 4 subnets with CIDR '10.0.0.0/24','10.0.1.0/24','10.0.4.0/22','10.0.8.0/22'?", 2, 10, groupNumber: 2)]
    [Test, Order(2)]
    public async Task Test02_VpcOf4Subnets()
    {
        DescribeSubnetsRequest describeSubnetsRequest = new();
        describeSubnetsRequest.Filters.Add(new Filter("vpc-id", [VpcId]));
        var describeSubnetsResponse = await AcctEc2Client!.DescribeSubnetsAsync(describeSubnetsRequest);
        Assert.That(describeSubnetsResponse.Subnets.Count(), Is.EqualTo(4));
        var expectedCidrAddresses = new string[] { "10.0.0.0/24", "10.0.1.0/24", "10.0.4.0/22", "10.0.8.0/22" };
        List<string> acturalCidrAddresses = describeSubnetsResponse.Subnets.Select(c => c.CidrBlock).ToList();
        Assert.That(acturalCidrAddresses, Is.EquivalentTo(expectedCidrAddresses));
    }

    [GameTask("In 'Cloud Project VPC', Can you create route tables for 4 individual subnets plus one local route only main route table?", 2, 10, groupNumber: 3)]
    [Test, Order(3)]
    public async Task Test03_VpcOf5RouteTable()
    {
        DescribeRouteTablesRequest describeRouteTablesRequest = new();
        describeRouteTablesRequest.Filters.Add(new Filter("vpc-id", [VpcId]));
        var describeRouteTablesResponse = await AcctEc2Client!.DescribeRouteTablesAsync(describeRouteTablesRequest);
        Assert.That(describeRouteTablesResponse.RouteTables, Has.Count.EqualTo(5));
    }


    [GameTask("In 'Cloud Project VPC', No subnet assocaietes to the Main RouteTable and it contains only one local route.", 2, 10, groupNumber: 4)]
    [Test, Order(4)]
    public async Task Test04_VpcMainRouteTable()
    {
        DescribeRouteTablesRequest describeRouteTablesRequest = new();
        describeRouteTablesRequest.Filters.Add(new Filter("vpc-id", [VpcId]));
        var describeRouteTablesResponse = await AcctEc2Client!.DescribeRouteTablesAsync(describeRouteTablesRequest);
        var routeTables = describeRouteTablesResponse.RouteTables;
        var mainRouteTable = routeTables.FirstOrDefault(c => c.Routes.Count == 1);
        Assert.That(mainRouteTable, Is.Not.Null);
        Assert.That(mainRouteTable!.Associations, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(mainRouteTable!.Associations[0].Main, Is.True);
            Assert.That(mainRouteTable!.Associations[0].SubnetId, Is.Null);
            Assert.That(mainRouteTable.Routes, Has.Count.EqualTo(1));
            Assert.That(mainRouteTable.Routes[0].DestinationCidrBlock, Is.EqualTo("10.0.0.0/16"));
            Assert.That(mainRouteTable.Routes[0].GatewayId, Is.EqualTo("local"));
        });
    }


}

