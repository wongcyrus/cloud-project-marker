using Amazon.SecurityToken;
using NUnit.Framework;
using ProjectTestsLib.Helper;
namespace ProjectTestsLib;

[GameClass(1), CancelAfter(Constants.Timeout), Order(1)]
public class CredentialTest
{


    [GameTask("Submit your AWS Academy Leaner Lab credentials.", 2, 10)]
    [Test]
    public async Task Test01_ValidCredential()
    {
        var credentialHelper = new CredentialHelper();
        var credential = credentialHelper.GetCredential();

        AmazonSecurityTokenServiceClient client = new(credential);
        var response = await client.GetCallerIdentityAsync(new Amazon.SecurityToken.Model.GetCallerIdentityRequest());     
        Assert.That(response.Account, Is.Not.Null);
        TestContext.Out.Write(response.Account);
    }
}

