import { expect } from "chai";
// if you used the '@types/mocha' method to install mocha type definitions, uncomment the following line
import "mocha";
import * as AWS from "aws-sdk";
import { SQS, SNS } from "aws-sdk";

describe("SQS and SNS", () => {
  const sqs: AWS.SQS = new AWS.SQS();
  const sns: AWS.SNS = new AWS.SNS();

  it("should have 2 SQS queues. ", async function () {
    this.timeout(10000);
    
    const errorQueue = await sqs
      .listQueues({ QueueNamePrefix: "Error_Queue" })
      .promise();
    const processQueue = await sqs
      .listQueues({
        QueueNamePrefix: "To_Be_Processed_Queue.fifo",
      })
      .promise();
    expect(1, "Error_Queue exist.").to.equal(errorQueue.QueueUrls!.length);
    expect(1, "To_Be_Processed_Queue exist.").to.equal(
      processQueue.QueueUrls!.length
    );
  });

  it("To_Be_Processed_Queue should be FIFO", async function () {
    this.timeout(10000);

    const processQueueUrl = (
      await sqs
        .listQueues({
          QueueNamePrefix: "To_Be_Processed_Queue.fifo",
        })
        .promise()
    ).QueueUrls![0];
    const processQueueAttributes: SQS.Types.GetQueueAttributesResult = await sqs
      .getQueueAttributes({
        QueueUrl: processQueueUrl,
        AttributeNames: ["FifoQueue"],
      })
      .promise();

    console.log(JSON.stringify(processQueueAttributes));

    // console.log(processQueueAttributes);
    const isFifoQueue: boolean = JSON.parse(processQueueAttributes!.Attributes!.FifoQueue);
    expect(isFifoQueue,
      "To_Be_Processed_Queue is FifoQueue."
    ).to.be.true;
  });

  it("To_Be_Processed_Queue should have 300 seconds VisibilityTimeout. ", async function () {
    this.timeout(10000);

    const processQueueUrl = (
      await sqs
        .listQueues({
          QueueNamePrefix: "To_Be_Processed_Queue.fifo",
        })
        .promise()
    ).QueueUrls![0];
    const processQueueAttributes: SQS.Types.GetQueueAttributesResult = await sqs
      .getQueueAttributes({
        QueueUrl: processQueueUrl,
        AttributeNames: ["VisibilityTimeout"],
      })
      .promise();

    // console.log(processQueueAttributes);
    const visibilityTimeout: number = +processQueueAttributes!.Attributes!
      .VisibilityTimeout;
    expect(
      300,
      "To_Be_Processed_Queue 300 seconds VisibilityTimeout."
    ).to.equal(visibilityTimeout);
  });

  it("should have Error Topic with Error_Queue subscription.", async function () {
    this.timeout(10000);
    const topics: SNS.Types.ListTopicsResponse = await sns
      .listTopics()
      .promise();

    const errorTopicArn = topics!.Topics!.find((c) =>
      c.TopicArn!.endsWith("ErrorTopic")
    )!.TopicArn;
    expect(errorTopicArn, "Error Topic exists.").to.be.exist;

    const subscriptions = await sns
      .listSubscriptionsByTopic({ TopicArn: errorTopicArn! })
      .promise();
    const errorQueueSubscription = subscriptions!.Subscriptions!.find(
      (c) => c.Protocol === "sqs" && c.Endpoint!.endsWith("Error_Queue")
    );

    expect(errorQueueSubscription, "Error Queue Subscription exists.").to.be
      .exist;
  });
});
