using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Common;
using NUnitLite;
using ProjectTestLib;


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

    // GET api/books
    [HttpGet]
    public async Task<ActionResult<string>> Get(
        [FromQuery(Name = "aws_access_key")] string accessKeyId,
        [FromQuery(Name = "aws_secret_access_key")] string secretAccessKey,
        [FromQuery(Name = "aws_session_token")] string sessionToken,
        [FromQuery] string region = "us-east-1",
        [FromQuery] string graderParameter = "")
    {
        logger.LogInformation("GraderController.Get called");
        if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey) || string.IsNullOrEmpty(region) || string.IsNullOrEmpty(sessionToken))
        {
            return BadRequest("Invalid request");
        }

        var xml = await RunUnitTestProcess(accessKeyId, "cywong", "");

        return Ok(xml);
    }

    private static string GetTemporaryDirectory(string trace)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), Math.Abs(trace.GetHashCode()).ToString());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }
    private async Task<string?> RunUnitTestProcess(string credentials, string trace, string filter)
    {
        var tempDir = GetTemporaryDirectory(trace);
        var tempCredentialsFilePath = Path.Combine(tempDir, "azureauth.json");

        await System.IO.File.WriteAllLinesAsync(tempCredentialsFilePath, new string[] { credentials });

        string workingDirectoryInfo = Environment.ExpandEnvironmentVariables(@"%HOME%\data\Functions\Tests");
        string exeLocation = Path.Combine(workingDirectoryInfo, "AzureProjectTest.exe");
        logger.LogInformation("Unit Test Exe Location: " + exeLocation);


        if (string.IsNullOrEmpty(filter))
            filter = "test==ProjectTestLib";
        else
        {
            // var serializerSettings = new JsonSerializerSettings
            // {
            //     ContractResolver = new CamelCasePropertyNamesContractResolver()
            // };
            // var jsonText = GameTaskFunction.GetTasksJson(false);
            // var json = JsonConvert.DeserializeObject<List<GameTaskData>>(jsonText, serializerSettings);
            // filter = json.First(c => c.Name == filter).Filter;

            // TODO: Implement filter
            filter = "test==ProjectTestLib";
        }

        logger.LogInformation($@"{tempCredentialsFilePath} {tempDir} {trace} {filter}");


        var where = filter;
        logger.LogInformation("tempCredentialsFilePath:" + tempCredentialsFilePath);
        logger.LogInformation("trace:" + trace);
        logger.LogInformation("where:" + where);


        var strWriter = new StringWriter();
        var autoRun = new AutoRun(typeof(Constants).GetTypeInfo().Assembly);

        var runTestParameters = new List<string>
        {
            "/test:ProjectTestLib",
            "--work=" + tempDir,
            "--output=" + tempDir,
            "--err=" + tempDir,
            "--params:AzureCredentialsPath=" + tempCredentialsFilePath + ";trace=" + trace
        };
        if (!string.IsNullOrEmpty(where)) runTestParameters.Insert(1, "--where=" + where);
        logger.LogInformation(runTestParameters.ToString());
        var returnCode = autoRun.Execute(runTestParameters.ToArray(), new ExtendedTextWrapper(strWriter), Console.In);

        var xml = await System.IO.File.ReadAllTextAsync(Path.Combine(tempDir, "TestResult.xml"));

        logger.LogInformation(strWriter.ToString());
        logger.LogInformation(xml);
        if (returnCode == 0)
        {
            return xml;
        }
        return null;
    }

}
