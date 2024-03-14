using System.Text;
using Amazon.AutoScaling;
using Amazon.AutoScaling.Model;
using Amazon.CloudWatch;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.SQS;
using NUnit.Framework;
using ProjectTestsLib.Helper;
namespace ProjectTestsLib;

[GameClass(7), CancelAfter(Constants.Timeout), Order(7)]
public class T08_AutoscalingTest : AwsTest
{
    private AmazonAutoScalingClient? AutoScalingClient { get; set; }
    private AmazonCloudWatchClient? CloudWatchClient { get; set; }
    private AmazonSQSClient? SqsClient { get; set; }
    private AmazonIdentityManagementServiceClient? IamClient { get; set; }
    private AmazonEC2Client? Ec2Client { get; set; }


    [SetUp]
    public new void Setup()
    {
        base.Setup();
        AutoScalingClient = new AmazonAutoScalingClient(Credential);
        CloudWatchClient = new AmazonCloudWatchClient(Credential);
        SqsClient = new AmazonSQSClient(Credential);
        IamClient = new AmazonIdentityManagementServiceClient(Credential);
        Ec2Client = new AmazonEC2Client(Credential);
    }

    [TearDown]
    public void TearDown()
    {
        AutoScalingClient?.Dispose();
        CloudWatchClient?.Dispose();
        SqsClient?.Dispose();
        IamClient?.Dispose();
        Ec2Client?.Dispose();
    }

    private async Task<AutoScalingGroup?> GetAutoScalinGroupByName(string groupName)
    {
        var group = await AutoScalingClient!.DescribeAutoScalingGroupsAsync(new DescribeAutoScalingGroupsRequest
        {
            AutoScalingGroupNames = [groupName],
        });
        return group.AutoScalingGroups?[0];
    }

    [GameTask("Create 'SqsAutoScalingGroup' Auto Scaling Group.", 2, 10)]
    [Test, Order(1)]
    public async Task Test01_SqsAutoScalingGroupExist()
    {
        AutoScalingGroup? autoScalingGroup = await GetAutoScalinGroupByName("SqsAutoScalingGroup");
        Assert.That(autoScalingGroup, Is.Not.Null);
    }

    [GameTask("'SqsAutoScalingGroup' Auto Scaling Group with settings: MinSize: 0, MaxSize: 5, DefaultCooldown: 300 seconds, HealthCheckType: 'EC2', HealthCheckGracePeriod: 300 seconds.", 2, 10)]
    [Test, Order(2)]
    public async Task Test02_SqsAutoScalingGroupSettings()
    {
        AutoScalingGroup? autoScalingGroup = await GetAutoScalinGroupByName("SqsAutoScalingGroup");

        Assert.That(autoScalingGroup, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(autoScalingGroup!.MinSize, Is.EqualTo(0));
            Assert.That(autoScalingGroup!.MaxSize, Is.EqualTo(5));
            Assert.That(autoScalingGroup!.DefaultCooldown, Is.EqualTo(300));
            Assert.That(autoScalingGroup!.HealthCheckType, Is.EqualTo("EC2"));
            Assert.That(autoScalingGroup!.HealthCheckGracePeriod, Is.EqualTo(300));
        });
    }

    [GameTask("'SqsAutoScalingGroup' LaunchTemplate Settings: Use LabRole, CpuCredits: unlimited, Key Pair: vockey, Instance Type: t3.nano, AMI Description: Starts with 'Amazon Linux 2023'.", 2, 10)]
    [Test, Order(2)]
    public async Task Test03_LaunchTemplateSettings()
    {
        AutoScalingGroup? autoScalingGroup = await GetAutoScalinGroupByName("SqsAutoScalingGroup");
        Assert.That(autoScalingGroup, Is.Not.Null);
        Assert.That(autoScalingGroup!.LaunchTemplate, Is.Not.Null);

        var launchTemplate = autoScalingGroup!.LaunchTemplate!;
        var launchTemplateDataResponse = await Ec2Client!.DescribeLaunchTemplateVersionsAsync(
            new DescribeLaunchTemplateVersionsRequest
            {
                LaunchTemplateId = launchTemplate.LaunchTemplateId,
                Versions = [launchTemplate.Version],
            }
        );
        Assert.That(launchTemplateDataResponse.LaunchTemplateVersions, Has.Count.EqualTo(1));
        var launchTemplateData = launchTemplateDataResponse.LaunchTemplateVersions[0].LaunchTemplateData;
        Assert.That(launchTemplateData, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(launchTemplateData.CreditSpecification.CpuCredits, Is.EqualTo("unlimited"));
            Assert.That(launchTemplateData.KeyName, Is.EqualTo("vockey"));
            Assert.That(launchTemplateData.InstanceType, Is.EqualTo(InstanceType.T3Nano));
            Assert.That(launchTemplateData.ImageId, Is.EqualTo("ami-0d7a109bf30624c99"));
        });

        var instanceProfileResponse = await IamClient!.GetInstanceProfileAsync(new GetInstanceProfileRequest
        {
            InstanceProfileName = launchTemplateData.IamInstanceProfile.Arn.Split("/").Last()
        });

        Assert.That(instanceProfileResponse.InstanceProfile.Roles[0].Arn, Does.EndWith("LabRole"));

        var describeImagesRequest = new DescribeImagesRequest
        {
            ImageIds = [launchTemplateData.ImageId] // Set the ImageIds property
        }; // Create an instance of DescribeImagesRequest
        var describeImagesResponse = await Ec2Client!.DescribeImagesAsync(describeImagesRequest); // Use the instance in the method call
        var image = describeImagesResponse.Images.FirstOrDefault();
        Assert.That(image, Is.Not.Null);
        Assert.That(image.Description, Does.StartWith("Amazon Linux 2023"));

    }

    [GameTask("LaunchTemplate UserData", 2, 10)]
    [Test, Order(2)]
    public async Task Test04_UserData()
    {
        AutoScalingGroup? autoScalingGroup = await GetAutoScalinGroupByName("SqsAutoScalingGroup");
        Assert.That(autoScalingGroup, Is.Not.Null);
        Assert.That(autoScalingGroup!.LaunchTemplate, Is.Not.Null);

        var launchTemplate = autoScalingGroup!.LaunchTemplate!;
        var launchTemplateDataResponse = await Ec2Client!.DescribeLaunchTemplateVersionsAsync(
            new DescribeLaunchTemplateVersionsRequest
            {
                LaunchTemplateId = launchTemplate.LaunchTemplateId,
                Versions = [launchTemplate.Version],
            }
        );
        Assert.That(launchTemplateDataResponse.LaunchTemplateVersions, Has.Count.EqualTo(1));
        var launchTemplateData = launchTemplateDataResponse.LaunchTemplateVersions[0].LaunchTemplateData;
        Assert.That(launchTemplateData, Is.Not.Null);
        var describeImagesRequest = new DescribeImagesRequest
        {
            ImageIds = [launchTemplateData.ImageId] // Set the ImageIds property
        }; // Create an instance of DescribeImagesRequest
        var describeImagesResponse = await Ec2Client!.DescribeImagesAsync(describeImagesRequest); // Use the instance in the method call
        var image = describeImagesResponse.Images.FirstOrDefault();
        Assert.That(image, Is.Not.Null);
        Assert.That(image.Description, Does.StartWith("Amazon Linux 2023"));

        string queueUrl = QueryHelper.GetSqsQueueUrl(SqsClient!, "To_Be_Processed_Queue.fifo");
        string expectedUserData = $@"#!/bin/bash

yum update -y
yum -y install jq
aws configure set default.region {Region}
echo ""Get Message from {queueUrl}""
ec2InstanceId=$(ec2-metadata --instance-id | cut -d "" "" -f 2); 
echo $ec2InstanceId
LogGroupName=/cloudproject/batchprocesslog
aws logs create-log-stream --log-group-name $LogGroupName --log-stream-name $ec2InstanceId
while sleep 10
do
    MSG=$(aws sqs receive-message --queue-url {queueUrl})
    [ ! -z ""$MSG""  ] && echo ""$MSG"" | jq -r '.Messages[] | .ReceiptHandle' | (xargs -I {{}} aws sqs delete-message --queue-url {queueUrl} --receipt-handle {{}})
    Message=$(echo ""$MSG"" | jq -r '.Messages[] | .Body')
    echo $Message
    TimeStamp=`date ""+%s%N"" --utc`
    TimeStamp=`expr $TimeStamp / 1000000`
    echo $TimeStamp
    UploadSequenceToken=$(aws logs describe-log-streams --log-group-name ""$LogGroupName"" --query 'logStreams[?logStreamName==`'$ec2InstanceId'`].[uploadSequenceToken]' --output text)
    echo $UploadSequenceToken
    if [ ""$UploadSequenceToken"" != ""None"" ]
    then
        aws logs put-log-events --log-group-name ""$LogGroupName"" --log-stream-name ""$ec2InstanceId"" --log-events timestamp=$TimeStamp,message=""$Message"" --sequence-token $UploadSequenceToken
    else
        aws logs put-log-events --log-group-name ""$LogGroupName"" --log-stream-name ""$ec2InstanceId"" --log-events timestamp=$TimeStamp,message=""$Message""
    fi
done
";

        var decodedUserData = Encoding.UTF8.GetString(Convert.FromBase64String(launchTemplateData.UserData));
        Console.WriteLine(decodedUserData);
        Assert.That(decodedUserData.Trim().Replace("\n", "").Replace("\r", "").Replace(" ", "").Replace("\t", ""),
        Is.EqualTo(expectedUserData.Trim().Replace("\n", "").Replace("\r", "").Replace(" ", "").Replace("\t", "")));

    }


}