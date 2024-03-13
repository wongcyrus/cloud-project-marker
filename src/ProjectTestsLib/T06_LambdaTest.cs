
using Amazon.DynamoDBv2;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.SecretsManager;
using Amazon.SQS;
using NUnit.Framework;
using ProjectTestsLib.Helper;
namespace ProjectTestsLib;

[GameClass(6), CancelAfter(Constants.Timeout), Order(6)]
public class T06_LambdaTest : AwsTest
{
    private AmazonLambdaClient? LambdaClient { get; set; }
    private AmazonIdentityManagementServiceClient? IamClient { get; set; }
    private AmazonSQSClient? SqsClient { get; set; }
    private AmazonSecretsManagerClient? SecretsManagerClient { get; set; }
    private AmazonEC2Client? AcctEc2Client { get; set; }
    private AmazonDynamoDBClient? DynamoDBClient { get; set; }


    [SetUp]
    public new void Setup()
    {
        base.Setup();
        LambdaClient = new AmazonLambdaClient(Credential);
        IamClient = new AmazonIdentityManagementServiceClient(Credential);
        SqsClient = new AmazonSQSClient(Credential);
        SecretsManagerClient = new AmazonSecretsManagerClient(Credential);
        AcctEc2Client = new AmazonEC2Client(Credential);
        DynamoDBClient = new AmazonDynamoDBClient(Credential);
    }

    [TearDown]
    public void TearDown()
    {
        LambdaClient?.Dispose();
        IamClient?.Dispose();
        SqsClient?.Dispose();
        SecretsManagerClient?.Dispose();
        AcctEc2Client?.Dispose();
        DynamoDBClient?.Dispose();
    }

    [GameTask("Create AWS Lambda Layer with name 'WebLambdaLayer' with 'request' package in Python 3.12.", 2, 10)]
    [Test, Order(1)]
    public async Task Test01_FunctionLayer()
    {
        var request = new ListLayersRequest
        {
            CompatibleRuntime = "python3.12"
        };
        var layers = await LambdaClient!.ListLayersAsync(request);
        var layer = layers.Layers?.FirstOrDefault(x => x.LayerName == "WebLambdaLayer");
        Assert.That(layer, Is.Not.Null);
    }

    [GameTask("Create AWS Lambda function with name 'WebLambda', in Python 3.12., attaches 'WebLambdaLayer', handler 'server.lambda_handler', x-ray enabled, timeout 120 seconds, and RAM 256 mb.", 2, 10)]
    [Test, Order(2)]
    public async Task Test02_FunctionSettings()
    {
        var request = new ListLayersRequest
        {
            CompatibleRuntime = "python3.12"
        };
        var layers = await LambdaClient!.ListLayersAsync(request);
        var layer = layers.Layers?.FirstOrDefault(x => x.LayerName == "WebLambdaLayer");

        var lambdaFunction = await LambdaClient!.GetFunctionAsync("WebLambda");
        Assert.That(lambdaFunction, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(lambdaFunction.Configuration?.Handler, Is.EqualTo("server.lambda_handler"));
            Assert.That(lambdaFunction.Configuration?.Runtime, Is.EqualTo(Runtime.Python312));
            Assert.That(lambdaFunction.Configuration?.Timeout, Is.EqualTo(120));
            Assert.That(lambdaFunction.Configuration?.MemorySize, Is.EqualTo(256));
            Assert.That(lambdaFunction.Configuration?.Layers?.Count, Is.EqualTo(1));
            Assert.That(lambdaFunction.Configuration?.TracingConfig.Mode, Is.EqualTo(TracingMode.Active));
        });
        Assert.That(layer, Is.Not.Null);
        Assert.That(lambdaFunction.Configuration?.Layers[0].Arn.StartsWith(layer.LayerArn), Is.True);
    }

    [GameTask("Set 'WebLambda' Environment vairables for WebLambda - secretsManagerVpcEndpointPrimaryDNSName, sqsEndpointDnsEntry, queueUrl, dbSecretArn, and messageTableName", 2, 10)]
    [Test, Order(3)]
    public async Task Test03_FunctionEnvironmentVariables()
    {
        var lambdaFunction = await LambdaClient!.GetFunctionAsync("WebLambda");
        Assert.That(lambdaFunction, Is.Not.Null);
        Assert.Multiple(async () =>
        {
            var environmentVariables = lambdaFunction.Configuration?.Environment;
            Assert.That(environmentVariables, Is.Not.Null);
            Assert.That(environmentVariables!.Variables.ContainsKey("secretsManagerVpcEndpointPrimaryDNSName"), Is.True);
            Assert.That(environmentVariables!.Variables.ContainsKey("sqsEndpointDnsEntry"), Is.True);
            Assert.That(environmentVariables!.Variables.ContainsKey("queueUrl"), Is.True);
            Assert.That(environmentVariables!.Variables.ContainsKey("dbSecretArn"), Is.True);
            Assert.That(environmentVariables!.Variables.ContainsKey("messageTableName"), Is.True);

            var queueUrl = QueryHelper.GetSqsQueueUrl(SqsClient!, "To_Be_Processed_Queue.fifo");
            Assert.That(environmentVariables!.Variables["queueUrl"], Is.EqualTo(queueUrl));
            var response = QueryHelper.GetSecretValueById(SecretsManagerClient!, "MasterUserSecret");
            Assert.That(environmentVariables!.Variables["dbSecretArn"], Is.EqualTo(response.ARN));
            var secretsmanagerEndpoint = QueryHelper.GetEndPointByServiceName(AcctEc2Client!, "com.amazonaws.us-east-1.secretsmanager");
            Assert.That(environmentVariables!.Variables["secretsManagerVpcEndpointPrimaryDNSName"], Is.EqualTo("https://" + secretsmanagerEndpoint.DnsEntries[0].DnsName));
            var sqsEndpoint = QueryHelper.GetEndPointByServiceName(AcctEc2Client!, "com.amazonaws.us-east-1.sqs");
            Assert.That(environmentVariables!.Variables["sqsEndpointDnsEntry"], Is.EqualTo("https://" + sqsEndpoint.DnsEntries[0].DnsName));
            var messageTableName = await DynamoDBClient!.DescribeTableAsync("Message");
            Assert.That(environmentVariables!.Variables["messageTableName"], Is.EqualTo(messageTableName.Table.TableName));
        });

    }


    [GameTask("'WebLambda' is using 2 private subnets in 2 Availability Zones.'.", 2, 10)]
    [Test, Order(4)]
    public async Task Test04_FunctionSubnets()
    {
        var lambdaFunction = await LambdaClient!.GetFunctionAsync("WebLambda");

        var subnet = await AcctEc2Client!.DescribeSubnetsAsync(new DescribeSubnetsRequest
        {
            SubnetIds = lambdaFunction.Configuration.VpcConfig!.SubnetIds!.ToList()!
        });
        Assert.Multiple(() =>
        {
            Assert.That(subnet.Subnets?.Count, Is.EqualTo(2));
            Assert.That(subnet.Subnets![0].VpcId, Is.EqualTo(lambdaFunction.Configuration.VpcConfig.VpcId));
            Assert.That(subnet.Subnets![0].CidrBlock.EndsWith("/22"), Is.True);
            Assert.That(subnet.Subnets![1].VpcId, Is.EqualTo(lambdaFunction.Configuration.VpcConfig.VpcId));
            Assert.That(subnet.Subnets![1].CidrBlock.EndsWith("/22"), Is.True);
            Assert.That(subnet.Subnets![0].AvailabilityZone, Is.Not.EqualTo(subnet.Subnets![1].AvailabilityZone));
        });
    }

    [GameTask("'WebLambda' uses only 'Web Lambda Security Group'.", 2, 10)]
    [Test, Order(5)]
    public async Task Test05_FunctionSecurityGroup()
    {
        var lambdaFunction = await LambdaClient!.GetFunctionAsync("WebLambda");

        var securityGroups = await AcctEc2Client!.DescribeSecurityGroupsAsync(new DescribeSecurityGroupsRequest
        {
            GroupIds = lambdaFunction.Configuration.VpcConfig!.SecurityGroupIds!.ToList()!
        });
        Assert.Multiple(() =>
        {
            Assert.That(securityGroups.SecurityGroups?.Count, Is.EqualTo(1));
            Assert.That(securityGroups.SecurityGroups![0].GroupName, Is.EqualTo("Web Lambda Security Group"));
            Assert.That(securityGroups.SecurityGroups![0].VpcId, Is.EqualTo(lambdaFunction.Configuration.VpcConfig.VpcId));
        });
    }

    [GameTask("'WebLambda' uses 'LabRole'.", 2, 10)]
    [Test, Order(6)]
    public async Task Test06_FunctionIamRole()
    {
        var lambdaFunction = await LambdaClient!.GetFunctionAsync("WebLambda");

        var role = await IamClient!.GetRoleAsync(new GetRoleRequest
        {
            RoleName = lambdaFunction.Configuration.Role.Split("/")[1]
        });
        Assert.That(role.Role.RoleName, Is.EqualTo("LabRole"));
    }

    [GameTask("'WebLambda' sets resource based policy for ALB trigger only.", 2, 10)]
    [Test, Order(7)]
    public async Task Test07_FunctionResourcePolicy()
    {
        var policy = await LambdaClient!.GetPolicyAsync(new Amazon.Lambda.Model.GetPolicyRequest()
        {
            FunctionName = "WebLambda"
        }, CancellationToken.None);

        var lambdaFunction = await LambdaClient!.GetFunctionAsync("WebLambda");


        Assert.That(policy.Policy, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(policy.Policy, Does.Contain("elasticloadbalancing.amazonaws.com"));
            Assert.That(policy.Policy, Does.Contain("Allow"));
            Assert.That(policy.Policy, Does.Contain(lambdaFunction.Configuration.FunctionArn));
        });

    }


}