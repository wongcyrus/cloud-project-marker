using Amazon.DynamoDBv2;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Newtonsoft.Json;
using NUnit.Framework;
using ProjectTestsLib.Helper;
namespace ProjectTestsLib;

[GameClass(5), CancelAfter(Constants.Timeout), Order(5)]
public class T05_DatabaseTest : AwsTest
{
    private AmazonSecretsManagerClient? SecretsManagerClient { get; set; }
    private AmazonDynamoDBClient? DynamoDBClient { get; set; }

    [SetUp]
    public new void Setup()
    {
        base.Setup();
        SecretsManagerClient = new AmazonSecretsManagerClient(Credential);
        DynamoDBClient = new AmazonDynamoDBClient(Credential);
    }

    [TearDown]
    public void TearDown()
    {
        SecretsManagerClient?.Dispose();
        DynamoDBClient?.Dispose();
    }

    private class Secret
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable IDE1006 // Naming Styles
        public string password { get; set; }
        public string username { get; set; }
#pragma warning restore IDE1006 // Naming Styles        
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    }



    [GameTask("Create MasterUserSecret in AmazonSecretsManager with 'username' equals to 'dbroot' and a random 'password'.", 2, 10)]
    [Test, Order(1)]
    public async Task Test01_DatabaseSecrets()
    {

        GetSecretValueRequest request = new()
        {
            SecretId = "MasterUserSecret",
            VersionStage = "AWSCURRENT", // VersionStage defaults to AWSCURRENT if unspecified.
        };
        var response = await SecretsManagerClient!.GetSecretValueAsync(request);

        Secret? parsedSecret = JsonConvert.DeserializeObject<Secret>(response.SecretString);

        Assert.That(parsedSecret, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(parsedSecret?.username, Is.Not.Null);
            Assert.That(parsedSecret?.username, Is.EqualTo("dbroot"));
            Assert.That(parsedSecret?.password, Is.Not.Null);
        });
    }

    [GameTask("Create Amazon DynamoDB table 'Message' in 'on demend mode' with partition key 'message' and range key 'time'.", 2, 10)]
    [Test, Order(1)]
    public async Task Test02_MessageDynamoDBTable()
    {

        var response = await DynamoDBClient!.ListTablesAsync();
        var messageTableName = response.TableNames.FirstOrDefault(c => c.Contains("Message"));
        Assert.That(messageTableName, Is.Not.Null);

        var messageTable = await DynamoDBClient.DescribeTableAsync(messageTableName);

        Assert.That(messageTable.Table, Is.Not.Null);
        Console.WriteLine(messageTable.Table.AttributeDefinitions);

        Assert.Multiple(() =>
        {
            Assert.That(messageTable.Table.AttributeDefinitions[0].AttributeName, Is.EqualTo("message"));
            Assert.That(messageTable.Table.AttributeDefinitions[0].AttributeType, Is.EqualTo(ScalarAttributeType.S));
            Assert.That(messageTable.Table.AttributeDefinitions[1].AttributeName, Is.EqualTo("time"));
            Assert.That(messageTable.Table.AttributeDefinitions[1].AttributeType, Is.EqualTo(ScalarAttributeType.S));
        });

        Assert.Multiple(() =>
        {
            Assert.That(messageTable.Table.KeySchema[0].AttributeName, Is.EqualTo("message"));
            Assert.That(messageTable.Table.KeySchema[0].KeyType, Is.EqualTo(KeyType.HASH));
            Assert.That(messageTable.Table.KeySchema[1].AttributeName, Is.EqualTo("time"));
            Assert.That(messageTable.Table.KeySchema[1].KeyType, Is.EqualTo(KeyType.RANGE));
        });

        Assert.That(messageTable.Table.BillingModeSummary.BillingMode, Is.EqualTo(BillingMode.PAY_PER_REQUEST));
    }


}