using Microsoft.AspNetCore.Mvc;

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

        return Ok(sessionToken);
    }

}
