using Microsoft.AspNetCore.Mvc;

namespace DemoTaskManager.Api.Controllers;

public sealed record AboutInfoDto(string ApplicationName, string Description);

[ApiController]
[Route("api/about")]
public sealed class AboutController : ControllerBase
{
    [HttpGet]
    public ActionResult<AboutInfoDto> Get()
    {
        return Ok(new AboutInfoDto(
            "Demo Task Manager",
            "A task management demo built with Angular and ASP.NET Core."));
    }
}