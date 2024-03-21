using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace ProjectTestsLib.Helper;

public static class QueryHelper
{
    public static string GetVpcId(AmazonEC2Client ec2Client)
    {
        var describeVpcsRequest = new DescribeVpcsRequest();
        describeVpcsRequest.Filters.Add(new Amazon.EC2.Model.Filter("tag:Name", ["Cloud Project VPC"]));
        var describeVpcsResponse = ec2Client.DescribeVpcsAsync(describeVpcsRequest).Result;
        return describeVpcsResponse.Vpcs[0].VpcId;
    }

    public static VpcEndpoint GetEndPointByServiceName(AmazonEC2Client ec2Client, string serviceName)
    {
        var describeVpcEndpointsRequest = new DescribeVpcEndpointsRequest();
        describeVpcEndpointsRequest.Filters.Add(new Amazon.EC2.Model.Filter("vpc-id", [GetVpcId(ec2Client)]));
        describeVpcEndpointsRequest.Filters.Add(new Amazon.EC2.Model.Filter("service-name", [serviceName]));
        var describeVpcEndpointsResponse = ec2Client.DescribeVpcEndpointsAsync(describeVpcEndpointsRequest).Result;
        var vpcEndpoints = describeVpcEndpointsResponse.VpcEndpoints;
        return vpcEndpoints[0];
    }

    public static SecurityGroup? GetSecurityGroupByName(AmazonEC2Client ec2Client, string groupName)
    {
        var vpcId = GetVpcId(ec2Client);
        var request = new DescribeSecurityGroupsRequest
        {
            Filters =
            [
                new("vpc-id", [vpcId]),
                new ("group-name", [groupName])
            ]
        };
        var response = ec2Client.DescribeSecurityGroupsAsync(request).Result;
        return response.SecurityGroups.FirstOrDefault();
    }

    public static string GetSqsQueueUrl(AmazonSQSClient sqsClient, string queueName)
    {
        var request = new GetQueueUrlRequest
        {
            QueueName = queueName
        };
        var response = sqsClient.GetQueueUrlAsync(request).Result;
        return response.QueueUrl;
    }

    public static GetSecretValueResponse GetSecretValueById(AmazonSecretsManagerClient secretsManagerClient, string secretId)
    {
        var request = new GetSecretValueRequest
        {
            SecretId = secretId
        };
        var response = secretsManagerClient.GetSecretValueAsync(request).Result;
        return response;
    }

    public static Topic? GetSnsTopicByNameContain(AmazonSimpleNotificationServiceClient SnsClient, string topicName)
    {
        var topics = SnsClient!.ListTopicsAsync().Result;
        var topic = topics.Topics.FirstOrDefault(x => x.TopicArn.Contains(topicName));
        return topic;
    }

}