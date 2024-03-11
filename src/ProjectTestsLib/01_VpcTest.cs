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
        VpcId = QueryHelper.GetVpcId(AcctEc2Client);
    }

    [TearDown]
    public void TearDown()
    {
        AcctEc2Client?.Dispose();
    }

    [GameTask("Create a VPC with CIDR 10.0.0.0/16 and name it as 'Cloud Project VPC'.", 2, 10)]
    [Test, Order(1)]
    public async Task Test01_VpcExist()
    {
        var describeVpcsRequest = new DescribeVpcsRequest();
        describeVpcsRequest.Filters.Add(new Filter("tag:Name", ["Cloud Project VPC"]));
        var describeVpcsResponse = await AcctEc2Client!.DescribeVpcsAsync(describeVpcsRequest);

        Assert.That(describeVpcsResponse.Vpcs, Has.Count.EqualTo(1));
    }

    [GameTask("In 'Cloud Project VPC', Create 4 subnets with CIDR '10.0.0.0/24','10.0.1.0/24','10.0.4.0/22','10.0.8.0/22'.", 2, 10)]
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

    [GameTask("In 'Cloud Project VPC', Create route tables for 4 individual subnets plus one local route only main route table.", 2, 10)]
    [Test, Order(3)]
    public async Task Test03_VpcOf5RouteTable()
    {
        DescribeRouteTablesRequest describeRouteTablesRequest = new();
        describeRouteTablesRequest.Filters.Add(new Filter("vpc-id", [VpcId]));
        var describeRouteTablesResponse = await AcctEc2Client!.DescribeRouteTablesAsync(describeRouteTablesRequest);
        Assert.That(describeRouteTablesResponse.RouteTables, Has.Count.EqualTo(5));
    }


    [GameTask("In 'Cloud Project VPC', No subnet assocaietes to the Main RouteTable and it contains only one local route.", 2, 10)]
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

    [GameTask("In 'Cloud Project VPC', 2 public subnet assocaietes to 2 public RouteTables and it contains internet route.", 2, 10)]
    [Test, Order(5)]
    public async Task Test05_VpcPublicRouteTables()
    {
        DescribeRouteTablesRequest describeRouteTablesRequest = new();
        describeRouteTablesRequest.Filters.Add(new Filter("vpc-id", [VpcId]));
        var describeRouteTablesResponse = await AcctEc2Client!.DescribeRouteTablesAsync(describeRouteTablesRequest);
        var routeTables = describeRouteTablesResponse.RouteTables;
        var publicRouteTables = routeTables.FindAll(c => c.Routes.Count == 2);
        Assert.That(publicRouteTables, Has.Count.EqualTo(2));

        foreach (var routeTable in publicRouteTables!)
        {
            var routes = routeTable.Routes;
            Assert.That(routes, Is.Not.Null, "Routes is null");
            Assert.That(routes.Count, Is.EqualTo(2), "Not enough routes");

            var route0 = routes[0];
            Assert.That(route0, Is.Not.Null, "Route 0 is null");
            Assert.Multiple(() =>
            {
                Assert.That(route0.DestinationCidrBlock, Is.EqualTo("10.0.0.0/16"), "Route 0 CIDR block mismatch");
                Assert.That(route0.GatewayId, Is.EqualTo("local"), "Route 0 Gateway ID mismatch");
            });

            var route1 = routes[1];
            Assert.That(route1, Is.Not.Null, "Route 1 is null");
            Assert.Multiple(() =>
            {
                Assert.That(route1.DestinationCidrBlock, Is.EqualTo("0.0.0.0/0"), "Route 1 CIDR block mismatch");
                Assert.That(route1.GatewayId, Does.StartWith("igw-"), "Route 1 Gateway ID does not start with 'igw-'");
            });
        };
    }

    [GameTask("In 'Cloud Project VPC',Creates 2 Interface end points for SQS and Secrets Manager.", 2, 10)]
    [Test, Order(6)]
    public async Task Test06_VpcOf2VpcInterfaceEndpoints()
    {
        DescribeVpcEndpointsRequest describeVpcEndpointsRequest = new();
        describeVpcEndpointsRequest.Filters.Add(new Filter("vpc-id", [VpcId]));
        var describeVpcEndpointsResponse = await AcctEc2Client!.DescribeVpcEndpointsAsync(describeVpcEndpointsRequest);
        var vpcEndpoints = describeVpcEndpointsResponse.VpcEndpoints;

        var interfaceEndpoints = vpcEndpoints.FindAll(c => c.VpcEndpointType == VpcEndpointType.Interface);
        Assert.That(interfaceEndpoints, Has.Count.EqualTo(2));
        var expectedInterfaceEndpoints = new string[] { "com.amazonaws.us-east-1.sqs", "com.amazonaws.us-east-1.secretsmanager" };
        var actualInterfaceEndpoints = interfaceEndpoints.Select(c => c.ServiceName).ToList();
        Assert.That(actualInterfaceEndpoints.OrderBy(x => x), Is.EquivalentTo(expectedInterfaceEndpoints.OrderBy(x => x)));
    }

    [GameTask("In 'Cloud Project VPC',Creates 2 Gateway end points for S3 and Dynamodb.", 2, 10)]
    [Test, Order(7)]
    public async Task Test07_VpcOf2VpcGatewayEndpoints()
    {
        DescribeVpcEndpointsRequest describeVpcEndpointsRequest = new();
        describeVpcEndpointsRequest.Filters.Add(new Filter("vpc-id", [VpcId]));
        var describeVpcEndpointsResponse = await AcctEc2Client!.DescribeVpcEndpointsAsync(describeVpcEndpointsRequest);
        var vpcEndpoints = describeVpcEndpointsResponse.VpcEndpoints;

        var gatewayEndpoints = vpcEndpoints.FindAll(c => c.VpcEndpointType == VpcEndpointType.Gateway);
        Assert.That(gatewayEndpoints, Has.Count.EqualTo(2));
        var expectedgatewayEndpoints = new string[] { "com.amazonaws.us-east-1.s3", "com.amazonaws.us-east-1.dynamodb" };
        var actualgatewayEndpoints = gatewayEndpoints.Select(c => c.ServiceName).ToList();
        Assert.That(actualgatewayEndpoints.OrderBy(x => x), Is.EquivalentTo(expectedgatewayEndpoints.OrderBy(x => x)));
    }

    [GameTask("In 'Cloud Project VPC', 2 Isolated subnet assocaietes to 2 Isolated RouteTables and it contains local route, and 2 gateway endpoints routes.", 2, 10)]
    [Test, Order(8)]
    public async Task Test08_VpcIsolatedRouteTables()
    {
        DescribeRouteTablesRequest describeRouteTablesRequest = new();
        describeRouteTablesRequest.Filters.Add(new Filter("vpc-id", [VpcId]));
        var describeRouteTablesResponse = await AcctEc2Client!.DescribeRouteTablesAsync(describeRouteTablesRequest);
        var routeTables = describeRouteTablesResponse.RouteTables;
        var isolatedRouteTables = routeTables.FindAll(c => c.Routes.Count == 3);
        Assert.That(isolatedRouteTables, Has.Count.EqualTo(2));

        DescribeVpcEndpointsRequest describeVpcEndpointsRequest = new();
        describeVpcEndpointsRequest.Filters.Add(new Filter("vpc-id", [VpcId]));
        var describeVpcEndpointsResponse = await AcctEc2Client!.DescribeVpcEndpointsAsync(describeVpcEndpointsRequest);
        var vpcEndpoints = describeVpcEndpointsResponse.VpcEndpoints;

        var gatewayEndpoints = vpcEndpoints.FindAll(c => c.VpcEndpointType == VpcEndpointType.Gateway);
        var gatewayEndpointsId = gatewayEndpoints.Select(c => c.VpcEndpointId).ToList();

        foreach (var routeTable in isolatedRouteTables!)
        {
            var routes = routeTable.Routes;
            Assert.That(routes, Is.Not.Null, "Routes is null");
            Assert.That(routes, Has.Count.EqualTo(3), "Not enough routes");

            var route0 = routes[0];
            Assert.That(route0, Is.Not.Null, "Route 0 is null");
            Assert.Multiple(() =>
            {
                Assert.That(route0.DestinationCidrBlock, Is.EqualTo("10.0.0.0/16"), "Route 0 CIDR block mismatch");
                Assert.That(route0.GatewayId, Is.EqualTo("local"), "Route 0 Gateway ID mismatch");
            });

            var route1 = routes[1];
            Assert.That(route1, Is.Not.Null, "Route 1 is null");
            var route2 = routes[2];
            Assert.That(route2, Is.Not.Null, "Route 2 is null");

            var gateWayIds = new string[] { route1.GatewayId, route2.GatewayId };
            Assert.That(gateWayIds.OrderBy(x => x), Is.EquivalentTo(gatewayEndpointsId.OrderBy(x => x)));
        };
    }

    [GameTask("In 'Cloud Project VPC', 2 Isolated subnet assocaietes to 2 Isolated RouteTables and it contains local route, and 2 gateway endpoints routes.", 2, 10)]
    [Test, Order(8)]
    public async Task Test09_Vpc4SubnetsFor4IndependentRouteTables()
    {
        DescribeRouteTablesRequest describeRouteTablesRequest = new();
        describeRouteTablesRequest.Filters.Add(new Filter("vpc-id", [VpcId]));
        var describeRouteTablesResponse = await AcctEc2Client!.DescribeRouteTablesAsync(describeRouteTablesRequest);
        var routeTables = describeRouteTablesResponse.RouteTables.FindAll(c => c.Routes.Count != 1); //Remove main route table

        routeTables.Select(c => c.Associations).ToList()
        .ForEach(c => Assert.That(c, Has.Count.EqualTo(1), "One subnet should be associated with one route table"));

        var subnetIds = routeTables.Select(c => c.Associations[0].SubnetId).ToHashSet();
        Assert.That(subnetIds, Has.Count.EqualTo(4), "No subnet should be associated with more than one route table");
    }
}

