using Amazon.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;


namespace ProjectTestsLib.Helper
{
    public class AwsTestConfig(string accessKeyId, string secretAccessKey, string sessionToken, string region = "us-east-1", string graderParameter = "", string trace = "us-east-1", string filter = nameof(ProjectTestsLib))
    {
        public string AccessKeyId { get; set; } = accessKeyId;

        public string SecretAccessKey { get; set; } = secretAccessKey;

        public string SessionToken { get; set; } = sessionToken;

        public string Region { get; set; } = region;

        public string GraderParameter { get; set; } = graderParameter;

        public string Trace { get; set; } = trace;

        public string Filter { get; set; } = filter;
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }


    public class CredentialHelper
    {
        public AwsTestConfig? AwsTestConfig { get; private set; }

        public CredentialHelper()
        {
            var credentialPath = TestContext.Parameters.Get("AwsTestConfig", null);
            if (credentialPath == null && File.Exists("/workspaces/cloud-project-marker/CloudProjectMarker/events/awsTestConfig.json"))
            {
                credentialPath = "/workspaces/cloud-project-marker/CloudProjectMarker/events/awsTestConfig.json";
                // Set the default AWS region environment variable
                Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");
            }

            if (credentialPath != null)
            {
                credentialPath = credentialPath.Trim('\'');
                var awsTestConfigString = File.ReadAllText(credentialPath);
                AwsTestConfig = JsonConvert.DeserializeObject<AwsTestConfig>(awsTestConfigString);
            }
        }


        public SessionAWSCredentials GetCredential()
        {
            return new SessionAWSCredentials(AwsTestConfig?.AccessKeyId, AwsTestConfig?.SecretAccessKey, AwsTestConfig?.SessionToken);
        }

        public string GetRegion()
        {
            return AwsTestConfig?.Region!;
        }
    }
}
