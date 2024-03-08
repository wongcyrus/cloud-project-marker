using Amazon.SecurityToken;
using Amazon.Runtime;
using NUnit.Framework;
using ProjectTestLib.Helper;
namespace ProjectTestLib;

[GameClass(1), CancelAfter(Constants.Timeout), Order(1)]
public class CredentialTest
{
   

    [GameTask("Can you submit your AWS Academy Leaner Lab credentials?", 2, 10, 1)]
    [Test]
    public async Task Test01_ValidCredentialAsync()
    {
        var credentialHelper = new CredentialHelper();
        var credential = credentialHelper.GetCredential();      

        AmazonSecurityTokenServiceClient client = new AmazonSecurityTokenServiceClient(credential);

        var response = await client.GetCallerIdentityAsync(new Amazon.SecurityToken.Model.GetCallerIdentityRequest());
        Console.WriteLine(response.Account);
        
        Assert.That(response.Account, Is.Not.Null);
    }
}

