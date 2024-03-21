using Amazon.AutoScaling;
using Amazon.CloudWatch;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.IdentityManagement;
using Amazon.Lambda;
using Amazon.SQS;
using NUnit.Framework;
using ProjectTestsLib.Helper;
namespace ProjectTestsLib;

[GameClass(9), CancelAfter(Constants.Timeout), Order(9)]
public class T09_ElasticLoadBalancingTest : AwsTest
{
    private AmazonAutoScalingClient? AutoScalingClient { get; set; }
    private AmazonCloudWatchClient? CloudWatchClient { get; set; }
    private AmazonSQSClient? SqsClient { get; set; }
    private AmazonIdentityManagementServiceClient? IamClient { get; set; }
    private AmazonEC2Client? Ec2Client { get; set; }

    private AmazonElasticLoadBalancingV2Client? ElasticLoadBalancingV2Client { get; set; }

    private AmazonLambdaClient? LambdaClient { get; set; }


    [SetUp]
    public new void Setup()
    {
        base.Setup();
        AutoScalingClient = new AmazonAutoScalingClient(Credential);
        CloudWatchClient = new AmazonCloudWatchClient(Credential);
        SqsClient = new AmazonSQSClient(Credential);
        IamClient = new AmazonIdentityManagementServiceClient(Credential);
        Ec2Client = new AmazonEC2Client(Credential);
        ElasticLoadBalancingV2Client = new AmazonElasticLoadBalancingV2Client(Credential);
        LambdaClient = new AmazonLambdaClient(Credential);
    }

    [TearDown]
    public void TearDown()
    {
        AutoScalingClient?.Dispose();
        CloudWatchClient?.Dispose();
        SqsClient?.Dispose();
        IamClient?.Dispose();
        Ec2Client?.Dispose();
        ElasticLoadBalancingV2Client?.Dispose();
        LambdaClient?.Dispose();
    }
    private async Task<LoadBalancer?> GetElasticLoadBalancerByName(string albName)
    {
        var loadBalancersResponse = await ElasticLoadBalancingV2Client!.DescribeLoadBalancersAsync(new Amazon.ElasticLoadBalancingV2.Model.DescribeLoadBalancersRequest
        {
            Names = [albName],
        });
        return loadBalancersResponse?.LoadBalancers?[0];
    }

    [GameTask("Create Application Load Balancer named 'WebAlb'.", 2, 10)]
    [Test, Order(1)]
    public async Task Test01_ElasticLoadBalancerExist()
    {
        LoadBalancer? alb = await GetElasticLoadBalancerByName("WebAlb");
        Assert.That(alb, Is.Not.Null);
    }

    [GameTask("Application Load Balancer 'WebAlb' is internet facing in IP verison 4.", 2, 10)]
    [Test, Order(2)]
    public async Task Test02_ElasticLoadBalancerInternetFacing()
    {
        LoadBalancer? alb = await GetElasticLoadBalancerByName("WebAlb");
        Assert.That(alb, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(alb!.Scheme, Is.EqualTo(LoadBalancerSchemeEnum.InternetFacing));
            Assert.That(alb!.Type, Is.EqualTo(LoadBalancerTypeEnum.Application));
            Assert.That(alb!.IpAddressType, Is.EqualTo(Amazon.ElasticLoadBalancingV2.IpAddressType.Ipv4));
        });

    }

    [GameTask("Application Load Balancer 'WebAlb' uses 2 public subnets.", 2, 10)]
    [Test, Order(3)]
    public async Task Test03_ElasticLoadBalancerInTwoPublicSubnet()
    {
        LoadBalancer? alb = await GetElasticLoadBalancerByName("WebAlb");
        Assert.That(alb, Is.Not.Null);
        Assert.That(alb!.AvailabilityZones, Has.Count.EqualTo(2));
        var subnetIds = alb!.AvailabilityZones.Select(az => az.SubnetId).Distinct().ToList();
        Assert.That(subnetIds, Has.Count.EqualTo(2));

        DescribeSubnetsRequest describeSubnetsRequest = new();
        describeSubnetsRequest.Filters.Add(new Filter("subnet-id", subnetIds));
        var describeSubnetsResponse = await Ec2Client!.DescribeSubnetsAsync(describeSubnetsRequest);
        Assert.That(describeSubnetsResponse!.Subnets, Has.Count.EqualTo(2));

        Assert.Multiple(() =>
        {
            Assert.That(describeSubnetsResponse!.Subnets?.Count, Is.EqualTo(2));
            Assert.That(describeSubnetsResponse.Subnets![0].CidrBlock, Does.EndWith("/24"));
            Assert.That(describeSubnetsResponse.Subnets![1].CidrBlock, Does.EndWith("/24"));
            Assert.That(describeSubnetsResponse.Subnets![0].AvailabilityZone, Is.Not.EqualTo(describeSubnetsResponse.Subnets![1].AvailabilityZone));
        });
    }

    [GameTask("Application Load Balancer 'WebAlb' has 1 listener for port 80 for HTTP.", 2, 10)]
    [Test, Order(4)]
    public async Task Test04_ElasticLoadBalancerListener()
    {
        LoadBalancer? alb = await GetElasticLoadBalancerByName("WebAlb");
        Assert.That(alb, Is.Not.Null);

        var listeners = await ElasticLoadBalancingV2Client!.DescribeListenersAsync(new DescribeListenersRequest
        {
            LoadBalancerArn = alb!.LoadBalancerArn,
        });
        Assert.That(listeners!.Listeners, Has.Count.EqualTo(1));
        var listener = listeners.Listeners[0];
        Assert.Multiple(() =>
        {
            Assert.That(listener.Port, Is.EqualTo(80));
            Assert.That(listener.Protocol, Is.EqualTo(ProtocolEnum.HTTP));
        });
    }

    [GameTask("Application Load Balancer 'WebAlb' listener Default Action is forward to a target group.", 2, 10)]
    [Test, Order(5)]
    public async Task Test05_ElasticLoadBalancerListenerDefaultAction()
    {
        LoadBalancer? alb = await GetElasticLoadBalancerByName("WebAlb");
        Assert.That(alb, Is.Not.Null);

        var listeners = await ElasticLoadBalancingV2Client!.DescribeListenersAsync(new DescribeListenersRequest
        {
            LoadBalancerArn = alb!.LoadBalancerArn,
        });
        Assert.That(listeners!.Listeners, Has.Count.EqualTo(1));
        var listener = listeners.Listeners[0];
        Assert.Multiple(() =>
        {
            Assert.That(listener.DefaultActions, Has.Count.EqualTo(1));
            var defaultAction = listener.DefaultActions[0];
            Assert.That(defaultAction.Type, Is.EqualTo(ActionTypeEnum.Forward));
            Assert.That(defaultAction.TargetGroupArn, Is.Not.Null);
            Assert.That(defaultAction.ForwardConfig.TargetGroups, Has.Count.EqualTo(1));
            Assert.That(defaultAction.ForwardConfig.TargetGroups[0].TargetGroupArn, Is.Not.Null);
            Assert.That(defaultAction.ForwardConfig.TargetGroups[0].Weight, Is.EqualTo(1));
            Assert.That(defaultAction.ForwardConfig.TargetGroupStickinessConfig.Enabled, Is.False);
        });
    }

    [GameTask("Application Load Balancer 'WebAlb' 2 target groups for web lambda in all availability zones and IP target to 10.0.0.4 and 10.0.1.4.", 2, 10)]
    [Test, Order(6)]
    public async Task Test06_ElasticLoadBalancerWithTwoTargetGroups()
    {
        LoadBalancer? alb = await GetElasticLoadBalancerByName("WebAlb");
        Assert.That(alb, Is.Not.Null);

        var targetGroups = await ElasticLoadBalancingV2Client!.DescribeTargetGroupsAsync(new DescribeTargetGroupsRequest
        {
            LoadBalancerArn = alb!.LoadBalancerArn,
        });
        Assert.That(targetGroups!.TargetGroups, Has.Count.EqualTo(2));
        var lambdaTargetGroup = targetGroups.TargetGroups.FirstOrDefault(tg => tg.TargetType == TargetTypeEnum.Lambda);
        var ipTargetGroup = targetGroups.TargetGroups.FirstOrDefault(tg => tg.TargetType == TargetTypeEnum.Ip);

        var lambdaFunction = await LambdaClient!.GetFunctionAsync("WebLambda");
        Assert.That(lambdaFunction, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(lambdaTargetGroup, Is.Not.Null);
            Assert.That(ipTargetGroup, Is.Not.Null);
        });

        var ipTargetHealth = await ElasticLoadBalancingV2Client.DescribeTargetHealthAsync(new DescribeTargetHealthRequest
        {
            TargetGroupArn = ipTargetGroup!.TargetGroupArn,
        });
        Assert.That(ipTargetHealth!.TargetHealthDescriptions, Has.Count.EqualTo(2));

        var targetIps = ipTargetHealth!.TargetHealthDescriptions.Select(t => t.Target!.Id).ToList();
        var expectedDummyIpTargets = new[] { "10.0.0.4", "10.0.1.4" };
        Assert.That(targetIps, Is.EquivalentTo(expectedDummyIpTargets));

        var lambdaTargetHealth = await ElasticLoadBalancingV2Client.DescribeTargetHealthAsync(new DescribeTargetHealthRequest
        {
            TargetGroupArn = lambdaTargetGroup!.TargetGroupArn,
        });
        Assert.That(lambdaTargetHealth!.TargetHealthDescriptions, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(lambdaTargetHealth!.TargetHealthDescriptions[0].Target!.Id, Is.EqualTo(lambdaFunction!.Configuration!.FunctionArn));
            Assert.That(lambdaTargetHealth!.TargetHealthDescriptions[0].Target!.AvailabilityZone, Is.EqualTo("all"));
        });
    }

    [GameTask(
@"Application Load Balancer 'WebAlb' Lambda target group Health Check:
1.	HealthCheckPath: '/'
2.	HealthCheckIntervalSeconds: 300
3.	HealthCheckTimeoutSeconds: 30
4.	HealthyThresholdCount: 5
5.	UnhealthyThresholdCount: 2
6.	Matcher.HttpCode: '200'
7.	TargetType: TargetTypeEnum.Lambda", 2, 10)]
    [Test, Order(7)]
    public async Task Test07_ElasticLoadBalancerWithLambdaTargetGroupHealthCheckSettings()
    {
        LoadBalancer? alb = await GetElasticLoadBalancerByName("WebAlb");
        Assert.That(alb, Is.Not.Null);

        var targetGroups = await ElasticLoadBalancingV2Client!.DescribeTargetGroupsAsync(new DescribeTargetGroupsRequest
        {
            LoadBalancerArn = alb!.LoadBalancerArn,
        });
        Assert.That(targetGroups!.TargetGroups, Has.Count.EqualTo(2));
        var lambdaTargetGroup = targetGroups.TargetGroups.FirstOrDefault(tg => tg.TargetType == TargetTypeEnum.Lambda);
        var lambdaFunction = await LambdaClient!.GetFunctionAsync("WebLambda");
        Assert.That(lambdaFunction, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(lambdaTargetGroup, Is.Not.Null);
            Assert.That(lambdaTargetGroup!.HealthCheckPath, Is.EqualTo("/"));
            Assert.That(lambdaTargetGroup!.HealthCheckIntervalSeconds, Is.EqualTo(300));
            Assert.That(lambdaTargetGroup!.HealthCheckTimeoutSeconds, Is.EqualTo(30));
            Assert.That(lambdaTargetGroup!.HealthyThresholdCount, Is.EqualTo(5));
            Assert.That(lambdaTargetGroup!.UnhealthyThresholdCount, Is.EqualTo(2));
            Assert.That(lambdaTargetGroup!.Matcher.HttpCode, Is.EqualTo("200"));
            Assert.That(lambdaTargetGroup!.TargetType, Is.EqualTo(TargetTypeEnum.Lambda));
        });
    }

    [GameTask(
@"Application Load Balancer 'WebAlb' IP target group Health Check:
1.	HealthCheckPath: '/'
2.	HealthCheckIntervalSeconds: 300
3.	HealthCheckTimeoutSeconds: 5
4.	HealthyThresholdCount: 5
5.	UnhealthyThresholdCount: 2
6.	HealthCheckProtocol: ProtocolEnum.HTTP
7.	HealthCheckPort: 'traffic-port'
8.	Matcher.HttpCode: '200'
9.	TargetType: TargetTypeEnum.Ip", 2, 10)]
    [Test, Order(8)]
    public async Task Test08_ElasticLoadBalancerWithIpTargetGroupHealthCheckSettings()
    {
        LoadBalancer? alb = await GetElasticLoadBalancerByName("WebAlb");
        Assert.That(alb, Is.Not.Null);

        var targetGroups = await ElasticLoadBalancingV2Client!.DescribeTargetGroupsAsync(new DescribeTargetGroupsRequest
        {
            LoadBalancerArn = alb!.LoadBalancerArn,
        });
        Assert.That(targetGroups!.TargetGroups, Has.Count.EqualTo(2));
        var ipTargetGroup = targetGroups.TargetGroups.FirstOrDefault(tg => tg.TargetType == TargetTypeEnum.Ip);

        Assert.Multiple(() =>
        {
            Assert.That(ipTargetGroup, Is.Not.Null);
            Assert.That(ipTargetGroup!.HealthCheckPath, Is.EqualTo("/"));
            Assert.That(ipTargetGroup!.HealthCheckIntervalSeconds, Is.EqualTo(300));
            Assert.That(ipTargetGroup!.HealthCheckTimeoutSeconds, Is.EqualTo(5));
            Assert.That(ipTargetGroup!.HealthyThresholdCount, Is.EqualTo(5));
            Assert.That(ipTargetGroup!.UnhealthyThresholdCount, Is.EqualTo(2));
            Assert.That(ipTargetGroup!.HealthCheckProtocol, Is.EqualTo(ProtocolEnum.HTTP));
            Assert.That(ipTargetGroup!.HealthCheckPort, Is.EqualTo("traffic-port"));
            Assert.That(ipTargetGroup!.Matcher.HttpCode, Is.EqualTo("200"));
            Assert.That(ipTargetGroup!.TargetType, Is.EqualTo(TargetTypeEnum.Ip));
        });
    }


    [GameTask(
@"Application Load Balancer 'WebAlb' Listener default rule:
1.	Priority: 'default'
2.	Actions: Has 1 item
3.	Conditions: Has 0 items
4.	Actions[0]: Not null
5.	Actions[0].Type: ActionTypeEnum.Forward
6.	Actions[0].TargetGroupArn: Equal to lambdaTargetGroup.TargetGroupArn
7.	Actions[0].ForwardConfig.TargetGroups: Has 1 item
8.	Actions[0].ForwardConfig.TargetGroups[0].TargetGroupArn: Equal to lambdaTargetGroup.TargetGroupArn
9.	Actions[0].ForwardConfig.TargetGroups[0].Weight: 1
10.	Actions[0].ForwardConfig.TargetGroupStickinessConfig.Enabled: False
", 2, 10)]
    [Test, Order(9)]
    public async Task Test09_ElasticLoadBalancerListenerDefaultRule()
    {
        LoadBalancer? alb = await GetElasticLoadBalancerByName("WebAlb");
        Assert.That(alb, Is.Not.Null);
        var listeners = await ElasticLoadBalancingV2Client!.DescribeListenersAsync(new DescribeListenersRequest
        {
            LoadBalancerArn = alb!.LoadBalancerArn,
        });
        Assert.That(listeners!.Listeners, Has.Count.EqualTo(1));
        var listener = listeners.Listeners[0];
        var rules = await ElasticLoadBalancingV2Client!.DescribeRulesAsync(new DescribeRulesRequest
        {
            ListenerArn = listener.ListenerArn,
        });
        Assert.That(rules!.Rules, Has.Count.EqualTo(2));

        var targetGroups = await ElasticLoadBalancingV2Client!.DescribeTargetGroupsAsync(new DescribeTargetGroupsRequest
        {
            LoadBalancerArn = alb!.LoadBalancerArn,
        });
        Assert.That(targetGroups!.TargetGroups, Has.Count.EqualTo(2));
        var lambdaTargetGroup = targetGroups.TargetGroups.FirstOrDefault(tg => tg.TargetType == TargetTypeEnum.Lambda);
        var lambdaFunction = await LambdaClient!.GetFunctionAsync("WebLambda");

        var defaultRule = rules.Rules.FirstOrDefault(r => r.IsDefault);
        Assert.That(defaultRule, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(defaultRule!.Priority, Is.EqualTo("default"));
            Assert.That(defaultRule!.Actions, Has.Count.EqualTo(1));
            Assert.That(defaultRule!.Conditions, Has.Count.EqualTo(0));
        });

        Assert.That(defaultRule!.Actions[0], Is.Not.Null);
        Assert.Multiple(() =>
        {

            Assert.That(defaultRule!.Actions[0].Type, Is.EqualTo(ActionTypeEnum.Forward));
            Assert.That(defaultRule!.Actions[0].TargetGroupArn, Is.EqualTo(lambdaTargetGroup!.TargetGroupArn));
        });

        Assert.Multiple(() =>
        {
            Assert.That(defaultRule!.Actions[0].ForwardConfig.TargetGroups, Has.Count.EqualTo(1));
            Assert.That(defaultRule!.Actions[0].ForwardConfig.TargetGroups[0].TargetGroupArn, Is.EqualTo(lambdaTargetGroup!.TargetGroupArn));
            Assert.That(defaultRule!.Actions[0].ForwardConfig.TargetGroups[0].Weight, Is.EqualTo(1));
            Assert.That(defaultRule!.Actions[0].ForwardConfig.TargetGroupStickinessConfig.Enabled, Is.False);
        });
    }

    [GameTask(
@"Application Load Balancer 'WebAlb' Listener default rule:

", 2, 10)]
    [Test, Order(10)]
    public async Task Test10_ElasticLoadBalancerDummyRule()
    {
        LoadBalancer? alb = await GetElasticLoadBalancerByName("WebAlb");
        Assert.That(alb, Is.Not.Null);
        var listeners = await ElasticLoadBalancingV2Client!.DescribeListenersAsync(new DescribeListenersRequest
        {
            LoadBalancerArn = alb!.LoadBalancerArn,
        });
        Assert.That(listeners!.Listeners, Has.Count.EqualTo(1));
        var listener = listeners.Listeners[0];
        var rules = await ElasticLoadBalancingV2Client!.DescribeRulesAsync(new DescribeRulesRequest
        {
            ListenerArn = listener.ListenerArn,
        });
        Assert.That(rules!.Rules, Has.Count.EqualTo(2));

        var targetGroups = await ElasticLoadBalancingV2Client!.DescribeTargetGroupsAsync(new DescribeTargetGroupsRequest
        {
            LoadBalancerArn = alb!.LoadBalancerArn,
        });
        var ipTargetGroup = targetGroups.TargetGroups.FirstOrDefault(tg => tg.TargetType == TargetTypeEnum.Ip);

        var dummyRule = rules.Rules.FirstOrDefault(r => !r.IsDefault);
        Assert.That(dummyRule, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dummyRule!.Priority, Is.EqualTo("10"));
            Assert.That(dummyRule!.Actions, Has.Count.EqualTo(1));
            Assert.That(dummyRule!.Conditions, Has.Count.EqualTo(1));
        });
        var condition = dummyRule!.Conditions[0];
        Assert.Multiple(() =>
        {
            Assert.That(condition.Field, Is.EqualTo("path-pattern"));
            Assert.That(condition.Values, Has.Count.EqualTo(2));
            var values = new[] { "/dummy", "/Dummy" };
            Assert.That(condition.Values, Is.EquivalentTo(values));
            Assert.That(condition.PathPatternConfig.Values, Is.EquivalentTo(values));
        });
        var action = dummyRule!.Actions[0];
        Assert.Multiple(() =>
        {
            Assert.That(action.Type, Is.EqualTo(ActionTypeEnum.Forward));
            Assert.That(action.TargetGroupArn, Is.EqualTo(ipTargetGroup!.TargetGroupArn));
            Assert.That(action.ForwardConfig.TargetGroups, Has.Count.EqualTo(1));
            Assert.That(action.ForwardConfig.TargetGroups[0].TargetGroupArn, Is.EqualTo(ipTargetGroup!.TargetGroupArn));
            Assert.That(action.ForwardConfig.TargetGroups[0].Weight, Is.EqualTo(1));
            Assert.That(action.ForwardConfig.TargetGroupStickinessConfig.Enabled, Is.False);
        });
    }
}