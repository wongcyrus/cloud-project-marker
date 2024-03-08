using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;
using NUnit.Framework;
using ProjectTestLib.Helper;
namespace ProjectTestLib;

[GameClass(2), CancelAfter(Constants.Timeout), Order(2)]
public class VpcTest
{
    private SessionAWSCredentials? Credential { get; set; }
    private AmazonEC2Client? AcctEc2Client { get; set; }

    [SetUp]
    public void Setup()
    {
        var credentialHelper = new CredentialHelper();
        Credential = credentialHelper.GetCredential();
        AcctEc2Client = new AmazonEC2Client(Credential);
    }

    [GameTask("Can you create a VPC with CIDR 10.0.0.0/16 and name it as 'Cloud Project VPC'?", 2, 10, groupNumber: 1)]
    [Test, Order(1)]
    public async Task Test01_VpcExist()
    {
        var describeVpcsRequest = new DescribeVpcsRequest();
        describeVpcsRequest.Filters.Add(new Filter("tag:Name", ["Cloud Project VPC"]));
        var describeVpcsResponse = await AcctEc2Client!.DescribeVpcsAsync(describeVpcsRequest);
        Console.WriteLine(describeVpcsResponse.Vpcs.Count());
        Assert.That(1, Is.Not.Null);
    }

    [GameTask("In 'Cloud Project VPC', Can you create 4 subnets with CIDR '10.0.0.0/24','10.0.1.0/24','10.0.4.0/22','10.0.8.0/22'?", 2, 10, groupNumber: 2)]
    [Test, Order(2)]
    public async Task Test02_VpcOf4Subnets()
    {
        var describeVpcsRequest = new DescribeVpcsRequest();
        describeVpcsRequest.Filters.Add(new Filter("tag:Name", ["Cloud Project VPC"]));
        var describeVpcsResponse = await AcctEc2Client!.DescribeVpcsAsync(describeVpcsRequest);
        var vpcId = describeVpcsResponse.Vpcs[0].VpcId;
        DescribeSubnetsRequest describeSubnetsRequest = new DescribeSubnetsRequest();
        describeSubnetsRequest.Filters.Add(new Filter("vpc-id", [vpcId]));
        var describeSubnetsResponse = await AcctEc2Client.DescribeSubnetsAsync(describeSubnetsRequest);
        Assert.That(describeSubnetsResponse.Subnets.Count(), Is.EqualTo(4));
        var expectedCidrAddresses = new string[] { "10.0.0.0/24", "10.0.1.0/24", "10.0.4.0/22", "10.0.8.0/22" };
        List<string> acturalCidrAddresses = describeSubnetsResponse.Subnets.Select(c => c.CidrBlock).ToList();
        Assert.That(acturalCidrAddresses, Is.EquivalentTo(expectedCidrAddresses));
    }

    [GameTask("In 'Cloud Project VPC', Can you create route tables for 4 individual subnets plus one local route only main route table?", 2, 10, groupNumber: 3)]
    [Test, Order(3)]
    public async Task Test03_1_VpcOf5RouteTable()
    {
        var describeVpcsRequest = new DescribeVpcsRequest();
        describeVpcsRequest.Filters.Add(new Filter("tag:Name", ["Cloud Project VPC"]));
        var describeVpcsResponse = await AcctEc2Client!.DescribeVpcsAsync(describeVpcsRequest);
        var vpcId = describeVpcsResponse.Vpcs[0].VpcId;
        DescribeRouteTablesRequest describeRouteTablesRequest = new DescribeRouteTablesRequest();
        describeRouteTablesRequest.Filters.Add(new Filter("vpc-id", [vpcId]));
        var describeRouteTablesResponse = await AcctEc2Client.DescribeRouteTablesAsync(describeRouteTablesRequest);
        Assert.That(describeRouteTablesResponse.RouteTables.Count(), Is.EqualTo(5));
    }


    [GameTask("In 'Cloud Project VPC', No subnet assocaietes to the Main RouteTable and it contains only one local route.", 2, 10, groupNumber: 4)]
    [Test, Order(4)]
    public async Task Test03_2_VpcOf5RouteTable()
    {
        var describeVpcsRequest = new DescribeVpcsRequest();
        describeVpcsRequest.Filters.Add(new Filter("tag:Name", ["Cloud Project VPC"]));
        var describeVpcsResponse = await AcctEc2Client!.DescribeVpcsAsync(describeVpcsRequest);
        var vpcId = describeVpcsResponse.Vpcs[0].VpcId;
        DescribeRouteTablesRequest describeRouteTablesRequest = new DescribeRouteTablesRequest();
        describeRouteTablesRequest.Filters.Add(new Filter("vpc-id", [vpcId]));
        var describeRouteTablesResponse = await AcctEc2Client.DescribeRouteTablesAsync(describeRouteTablesRequest);
        var routeTables = describeRouteTablesResponse.RouteTables;
        var mainRouteTable = routeTables.FirstOrDefault(c => c.Routes.Count == 1);
        Assert.That(mainRouteTable, Is.Not.Null);
        Assert.That(mainRouteTable!.Associations.Count, Is.EqualTo(0));
        Assert.That(mainRouteTable.Routes.Count(), Is.EqualTo(1));
        Assert.That(mainRouteTable.Routes[0].DestinationCidrBlock, Is.EqualTo("10.0.0.0/16"));
        
        Assert.That(mainRouteTable.Routes[0].Origin, Is.EqualTo("local"));



    }


}

