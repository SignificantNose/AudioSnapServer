using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AudioSnapServer.Controllers;

public class ErrorController : ControllerBase
{
    [Route("/error-template")]
    public IActionResult HandleErrorDevelopment(
        [FromServices] IHostEnvironment hostEnvironment
        )
    {
        // also possible
        if (!hostEnvironment.IsDevelopment())
        {
            return NotFound();
        }

        var exceptionHandlerFeature = HttpContext.Features.Get<IExceptionHandlerFeature>();
        return Problem(
            detail: exceptionHandlerFeature.Error.StackTrace,
            title: exceptionHandlerFeature.Error.Message
            );

    }

    [Route("/error")]
    public IActionResult HandleError()
    {
        // return 500: INTERNAL ERROR code
        return Problem();
    }
}