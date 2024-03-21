
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;

using Amazon.SimpleNotificationService;
using NUnit.Framework;
using ProjectTestsLib.Helper;
namespace ProjectTestsLib;

[GameClass(7), CancelAfter(Constants.Timeout), Order(7)]
public class T07_CloudWatchTest : AwsTest
{
    private AmazonCloudWatchClient? CloudWatchClient { get; set; }
    public AmazonCloudWatchLogsClient? CloudWatchLogsClient { get; set; }
    private AmazonSimpleNotificationServiceClient? SnsClient { get; set; }

    [SetUp]
    public new void Setup()
    {
        base.Setup();
        CloudWatchClient = new AmazonCloudWatchClient(Credential);
        CloudWatchLogsClient = new AmazonCloudWatchLogsClient(Credential);
        SnsClient = new AmazonSimpleNotificationServiceClient(Credential);
    }

    [TearDown]
    public void TearDown()
    {
        CloudWatchClient?.Dispose();
        CloudWatchLogsClient?.Dispose();
        SnsClient?.Dispose();
    }

    [GameTask("Create /cloudproject/batchprocesslog log group Retention period 7 days.", 2, 10)]
    [Test, Order(1)]
    public async Task Test01_BatchProcessLogGroup()
    {
        var describeLogGroupsRequest = new DescribeLogGroupsRequest()
        {
            LogGroupNamePrefix = "/cloudproject/batchprocesslog"
        };
        var batchprocesslogGroups = await CloudWatchLogsClient!.DescribeLogGroupsAsync(describeLogGroupsRequest);

        Assert.That(batchprocesslogGroups?.LogGroups.Count, Is.EqualTo(1));
        Assert.That(batchprocesslogGroups?.LogGroups[0].RetentionInDays, Is.EqualTo(7));
    }

    [GameTask("Create Metric Filter '?\"error\" ?\"ERROR\" ?\"Error\"' for /cloudproject/batchprocesslog log group.", 2, 10)]
    [Test, Order(2)]
    public async Task Test02_BatchProcessLogGroupMetricFilter()
    {
        var describeLogGroupsRequest = new DescribeLogGroupsRequest()
        {
            LogGroupNamePrefix = "/cloudproject/batchprocesslog"
        };
        var batchprocesslogGroups = await CloudWatchLogsClient!.DescribeLogGroupsAsync(describeLogGroupsRequest);

        Assert.That(batchprocesslogGroups?.LogGroups.Count, Is.EqualTo(1));
        var metricFilters = await CloudWatchLogsClient!.DescribeMetricFiltersAsync(new DescribeMetricFiltersRequest()
        {
            LogGroupName = "/cloudproject/batchprocesslog"
        });
        Assert.That(metricFilters?.MetricFilters.Count, Is.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(metricFilters?.MetricFilters[0].LogGroupName, Is.EqualTo("/cloudproject/batchprocesslog"));
            Assert.That(metricFilters?.MetricFilters[0].FilterPattern, Is.EqualTo("?\"error\" ?\"ERROR\" ?\"Error\""));
            Assert.That(metricFilters?.MetricFilters[0].MetricTransformations.Count, Is.EqualTo(1));
        });

        Assert.Multiple(() =>
        {
            Assert.That(metricFilters?.MetricFilters[0].MetricTransformations[0].MetricName, Is.EqualTo("error-message-count"));
            Assert.That(metricFilters?.MetricFilters[0].MetricTransformations[0].MetricNamespace, Is.EqualTo("cloudproject"));
            Assert.That(metricFilters?.MetricFilters[0].MetricTransformations[0].MetricValue, Is.EqualTo("1"));
        });
    }


    [GameTask(
@"Create 1 alarm for error terms in /cloudproject/batchprocesslog log group.
1.	AlarmName should be equal to 'alarms-batchProcessErrors'.
2.	AlarmDescription should be equal to 'Alarm batch Process Errors'.
3.	ActionsEnabled should be true.
4.	AlarmActions.Count should be 1.
5.	The first element in AlarmActions should be equal to errorTopic Arn.
6.	MetricName should be equal to 'error-message-count'.
7.	Namespace should be equal to 'cloudproject'.
8.	Statistic should be equal to Statistic.SampleCount.
9.	Threshold should be 0.
10.	ComparisonOperator should GreaterThanThreshold.
11.	Period should be 60.
12.	EvaluationPeriods should be 1.
13.	TreatMissingData should be equal to 'missing'.
14.	OKActions should be empty.
15.	InsufficientDataActions should be empty.
16.	Metrics should be empty.
", 2, 10)]
    [Test, Order(3)]
    public async Task Test03_BatchProcessLogGroupAlarmForError()
    {
        var errorAlarms = await CloudWatchClient!.DescribeAlarmsAsync(new DescribeAlarmsRequest()
        {
            AlarmNames = ["alarms-batchProcessErrors"]
        });
        Assert.That(errorAlarms?.MetricAlarms.Count, Is.EqualTo(1));
        var errorAlarm = errorAlarms?.MetricAlarms[0];
        var errorTopic = QueryHelper.GetSnsTopicByNameContain(SnsClient!, "ErrorTopic");

        Assert.Multiple(() =>
        {
            Assert.That(errorAlarm?.AlarmName, Is.EqualTo("alarms-batchProcessErrors"));
            Assert.That(errorAlarm?.AlarmDescription, Is.EqualTo("Alarm batch Process Errors"));
            Assert.That(errorAlarm?.ActionsEnabled, Is.True);
            Assert.That(errorAlarm?.AlarmActions.Count, Is.EqualTo(1));
            Assert.That(errorAlarm?.AlarmActions[0], Is.EqualTo(errorTopic!.TopicArn));
            Assert.That(errorAlarm?.MetricName, Is.EqualTo("error-message-count"));
            Assert.That(errorAlarm?.Namespace, Is.EqualTo("cloudproject"));
            Assert.That(errorAlarm?.Statistic, Is.EqualTo(Statistic.SampleCount));
            Assert.That(errorAlarm?.Threshold, Is.EqualTo(0));
            Assert.That(errorAlarm?.ComparisonOperator, Is.EqualTo(Amazon.CloudWatch.ComparisonOperator.GreaterThanThreshold));
            Assert.That(errorAlarm?.Period, Is.EqualTo(60));
            Assert.That(errorAlarm?.EvaluationPeriods, Is.EqualTo(1));
            Assert.That(errorAlarm?.TreatMissingData, Is.EqualTo("missing"));
            Assert.That(errorAlarm?.OKActions, Is.Empty);
            Assert.That(errorAlarm?.InsufficientDataActions, Is.Empty);
            Assert.That(errorAlarm?.Metrics, Is.Empty);
        });
    }


}