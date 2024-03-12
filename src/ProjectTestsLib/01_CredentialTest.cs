using Amazon.SecurityToken;
using NUnit.Framework;
using ProjectTestsLib.Helper;
namespace ProjectTestsLib;

[GameClass(1), CancelAfter(Constants.Timeout), Order(1)]
public class CredentialTest : AwsTest
{
    [SetUp]
    public new void Setup()
    {
        base.Setup();
    }

    [GameTask("Submit your AWS Academy Leaner Lab credentials.", 2, 10)]
    [Test]
    public async Task Test01_ValidCredential()
    {    
        AmazonSecurityTokenServiceClient client = new(Credential);
        var response = await client.GetCallerIdentityAsync(new Amazon.SecurityToken.Model.GetCallerIdentityRequest());     
        Assert.That(response.Account, Is.Not.Null);
        TestContext.Out.Write(response.Account);
    }
}

