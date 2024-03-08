using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Common;
using NUnitLite;
using ProjectTestLib;
using ProjectTestLib.Helper;


namespace ServerlessAPI.Controllers;

[Route("api/[controller]")]
[Produces("application/json")]
public class GraderController : ControllerBase
{
    private readonly ILogger<GraderController> logger;
    public GraderController(ILogger<GraderController> logger)
    {
        this.logger = logger;
    }

    // GET api/grader
    [HttpGet]
    public async Task<ActionResult<string>> Get(
        [FromQuery(Name = "aws_access_key")] string accessKeyId,
        [FromQuery(Name = "aws_secret_access_key")] string secretAccessKey,
        [FromQuery(Name = "aws_session_token")] string sessionToken,
        [FromQuery] string region = "us-east-1",
        [FromQuery] string trace = "anonymous",
        [FromQuery] string filter = "",
        [FromQuery] string graderParameter = "")
    {
        logger.LogInformation("GraderController.Get called");
        if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey) || string.IsNullOrEmpty(sessionToken))
        {
            return BadRequest("Invalid request");
        }

        var awsTestConfig = new AwsTestConfig(accessKeyId, secretAccessKey, sessionToken, region, graderParameter, trace, filter);
        var json = await RunUnitTest(awsTestConfig);

        logger.LogInformation(json);
        return Ok(json);
    }

    private static string GetTemporaryDirectory(string trace)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), Math.Abs(trace.GetHashCode()).ToString());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }
    private async Task<string?> RunUnitTest(AwsTestConfig awsTestConfig)
    {
        var tempDir = GetTemporaryDirectory(awsTestConfig.Trace);
        var credentials = JsonConvert.SerializeObject(awsTestConfig);
        var tempCredentialsFilePath = Path.Combine(tempDir, "awsTestConfig.json");

        await System.IO.File.WriteAllLinesAsync(tempCredentialsFilePath, [credentials]);

        var where = awsTestConfig.Filter;
        var trace = awsTestConfig.Trace;

        var serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        var jsonText = GameController.GetTasksJson();
        var json = JsonConvert.DeserializeObject<List<GameTaskData>>(jsonText, serializerSettings);
        var matchingTask = json?.FirstOrDefault(c => c.Name == where);
        where = matchingTask?.Filter ?? "test==" + nameof(ProjectTestLib);


        logger.LogInformation($@"{tempCredentialsFilePath} {trace} {where}");

        var strWriter = new StringWriter();
        var autoRun = new AutoRun(typeof(Constants).GetTypeInfo().Assembly);

        var runTestParameters = new List<string>
        {
            "/test:"+nameof(ProjectTestLib),
            "--work=" + tempDir,
            "--output=" + tempDir,
            "--err=" + tempDir,
            "--params:AwsTestConfig=" + tempCredentialsFilePath + ";trace=" + trace
        };
        runTestParameters.Insert(1, "--where=" + where);
        logger.LogInformation(string.Join(" ", runTestParameters));
        var returnCode = autoRun.Execute([.. runTestParameters], new ExtendedTextWrapper(strWriter), Console.In);
        logger.LogInformation("returnCode:" + returnCode);
        logger.LogInformation(strWriter.ToString());

        // logger.LogInformation(xml);
        if (returnCode == 0)
        {
            var xml = await System.IO.File.ReadAllTextAsync(Path.Combine(tempDir, "TestResult.xml"));
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            // return JsonConvert.SerializeXmlNode(doc);
            return xml;
        }
        return null;
    }

}
