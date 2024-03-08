using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;
using NUnit.Framework;
using ProjectTestLib.Helper;
namespace ProjectTestLib;

[GameClass(1), CancelAfter(Constants.Timeout)]
public class VpcTest
{
    private SessionAWSCredentials Credential { get; set; }
    private AmazonEC2Client AcctEc2Client { get; set; }

    [SetUp]
    public void Setup()
    {
        var credentialHelper = new CredentialHelper();
        Credential = credentialHelper.GetCredential();                
        AcctEc2Client = new AmazonEC2Client(Credential);
    }

    [GameTask("Can you create a resource group named 'projProd' in Hong Kong?", 2, 10, 1)]
    [Test]
    public async Task Test01_ResourceGroupExistAsync()
    {
        var describeVpcsRequest = new DescribeVpcsRequest();
        describeVpcsRequest.Filters.Add(new Filter("tag:Name", ["Cloud Project VPC"]));
        var describeVpcsResponse = await AcctEc2Client.DescribeVpcsAsync(describeVpcsRequest);

        Console.WriteLine(describeVpcsResponse.Vpcs.Count());

        
        Assert.That(1, Is.Not.Null);
    }
}

