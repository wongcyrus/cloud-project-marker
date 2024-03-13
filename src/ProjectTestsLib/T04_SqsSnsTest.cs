using Amazon.SimpleNotificationService;
using Amazon.SQS;
using NUnit.Framework;
using ProjectTestsLib.Helper;
using Amazon.SQS.Model;
using Amazon.SimpleNotificationService.Model;
namespace ProjectTestsLib;

[GameClass(4), CancelAfter(Constants.Timeout), Order(4)]
public class T04_SqsSnsTest : AwsTest
{
    private AmazonSimpleNotificationServiceClient? SnsClient { get; set; }
    private AmazonSQSClient? SqsClient { get; set; }

    [SetUp]
    public new void Setup()
    {
        base.Setup();
        SnsClient = new AmazonSimpleNotificationServiceClient(Credential);
        SqsClient = new AmazonSQSClient(Credential);
    }

    [TearDown]
    public void TearDown()
    {
        SnsClient?.Dispose();
        SqsClient?.Dispose();
    }

    [GameTask("Create 3 SQS queues 'To_Be_Processed_Queue.fifo', 'Dead_Letter_Queue.fifo' and 'Error_Queue'.", 2, 10)]
    [Test, Order(1)]
    public async Task Test01_3SqsQueues()
    {
        var errorQueue = await SqsClient!.ListQueuesAsync(queueNamePrefix: "Error_Queue");
        var processQueue = await SqsClient!.ListQueuesAsync(queueNamePrefix: "To_Be_Processed_Queue.fifo");
        var deadLetterQueue = await SqsClient!.ListQueuesAsync(queueNamePrefix: "Dead_Letter_Queue.fifo");

        Assert.Multiple(() =>
        {
            Assert.That(errorQueue, Is.Not.Null);
            Assert.That(errorQueue.QueueUrls, Has.Count.EqualTo(1));
            Assert.That(processQueue, Is.Not.Null);
            Assert.That(processQueue.QueueUrls, Has.Count.EqualTo(1));
            Assert.That(deadLetterQueue, Is.Not.Null);
            Assert.That(deadLetterQueue.QueueUrls, Has.Count.EqualTo(1));
        });
    }

    [GameTask("'To_Be_Processed_Queue.fifo' and 'Dead_Letter_Queue.fifo' queues are in 'first in first out'.", 2, 10)]
    [Test, Order(2)]
    public async Task Test02_ToBeProcessQueuesFiFo()
    {
        var processQueueAttribute = await SqsClient!.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = "To_Be_Processed_Queue.fifo",
            AttributeNames = ["FifoQueue"]
        });
        Assert.That(processQueueAttribute, Is.Not.Null);
        Assert.That(processQueueAttribute.Attributes, Contains.Key("FifoQueue"));
        Assert.That(processQueueAttribute.Attributes["FifoQueue"], Is.EqualTo("true"));

        var deadLetterQueueAttribute = await SqsClient!.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = "Dead_Letter_Queue.fifo",
            AttributeNames = ["FifoQueue"]
        });
        Assert.That(deadLetterQueueAttribute, Is.Not.Null);
        Assert.That(deadLetterQueueAttribute.Attributes, Contains.Key("FifoQueue"));
        Assert.That(deadLetterQueueAttribute.Attributes["FifoQueue"], Is.EqualTo("true"));
    }

    [GameTask("VisibilityTimeout settings: 1. 'To_Be_Processed_Queue.fifo' is 300 seconds. 2. 'Dead_Letter_Queue.fifo' is 12 hours. 3. Error_Queue is 30 seconds. ", 2, 10)]
    [Test, Order(3)]
    public async Task Test03_ToBeProcessQueuesVisibilityTimeout()
    {
        var processQueueAttribute = await SqsClient!.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = "To_Be_Processed_Queue.fifo",
            AttributeNames = ["VisibilityTimeout"]
        });
        Assert.That(processQueueAttribute, Is.Not.Null);
        Assert.That(processQueueAttribute.Attributes, Contains.Key("VisibilityTimeout"));
        Assert.That(processQueueAttribute.Attributes["VisibilityTimeout"], Is.EqualTo("300"));

        var deadLetterQueueAttribute = await SqsClient!.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = "Dead_Letter_Queue.fifo",
            AttributeNames = ["VisibilityTimeout"]
        });
        Assert.That(deadLetterQueueAttribute, Is.Not.Null);
        Assert.That(deadLetterQueueAttribute.Attributes, Contains.Key("VisibilityTimeout"));
        Assert.That(deadLetterQueueAttribute.Attributes["VisibilityTimeout"], Is.EqualTo("43200"));

        var errorQueueAttribute = await SqsClient!.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = "Error_Queue",
            AttributeNames = ["VisibilityTimeout"]
        });
        Assert.That(errorQueueAttribute, Is.Not.Null);
        Assert.That(errorQueueAttribute.Attributes, Contains.Key("VisibilityTimeout"));
        Assert.That(errorQueueAttribute.Attributes["VisibilityTimeout"], Is.EqualTo("30"));
    }

    [GameTask("'To_Be_Processed_Queue.fifo' queue uses 'Dead_Letter_Queue.fifo' queue as dead letter queue.", 2, 10)]
    [Test, Order(4)]
    public async Task Test04_ToBeProcessQueueSetsDeadLetterQueue()
    {
        var processQueueAttribute = await SqsClient!.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = "To_Be_Processed_Queue.fifo",
            AttributeNames = ["RedrivePolicy"]
        });
        Assert.That(processQueueAttribute, Is.Not.Null);
        Assert.That(processQueueAttribute.Attributes, Contains.Key("RedrivePolicy"));

        var deadLetterQueueAttribute = await SqsClient!.GetQueueAttributesAsync(new GetQueueAttributesRequest
        {
            QueueUrl = "Dead_Letter_Queue.fifo",
            AttributeNames = ["QueueArn"]
        });

        Assert.That(processQueueAttribute.Attributes["RedrivePolicy"], Does.Contain(deadLetterQueueAttribute.QueueARN));
    }

    [GameTask("'Error_Queue' queue subscribes to Amazon SNS 'Error Topic'. ", 2, 10)]
    [Test, Order(5)]
    public async Task Test05_ErrorTopicSubscribesToErrorQueue()
    {
        var errorTopic =  QueryHelper.GetSnsTopicByNameContain(SnsClient!, "ErrorTopic");       
        Assert.That(errorTopic, Is.Not.Null);
        var subscriptions = await SnsClient!.ListSubscriptionsByTopicAsync(new ListSubscriptionsByTopicRequest
        {
            TopicArn = errorTopic.TopicArn
        });
        Assert.That(subscriptions, Is.Not.Null);
        var errorQueueSubscription = subscriptions.Subscriptions.FirstOrDefault(x => x.Protocol == "sqs" && x.Endpoint.EndsWith("Error_Queue"));
        Assert.That(errorQueueSubscription, Is.Not.Null);
    }

}

