using Amazon.Runtime;

namespace ProjectTestsLib.Helper;


public abstract class AwsTest
{
    protected SessionAWSCredentials? Credential { get; set; }

    protected void Setup()
    {
        var credentialHelper = new CredentialHelper();
        Credential = credentialHelper.GetCredential();
    }
}