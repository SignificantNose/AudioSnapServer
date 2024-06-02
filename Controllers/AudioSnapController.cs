using System.Text.Json;
using AudioSnapServer.Models;
using AudioSnapServer.Services.AudioSnap;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AudioSnapServer.Controllers;

// Route attribute allows to get rid of writing
// the repetitive paths to the endpoints. Right 
// now, as we only allow 1 request, it's not that
// crucial, but who knows how much can any API change?
[ApiController]
[Route("snap")]
public class AudioSnapController : ControllerBase
{
    private IAudioSnapService _snapService;
    
    public AudioSnapController(IAudioSnapService snapService)
    {
        _snapService = snapService;
    }

    [HttpPost]
    public async Task<IActionResult> SnapFingerprint(AudioSnapClientQuery query)
    {
        _snapService.SetNeededComponents(query);
        if (!await _snapService.CalculateSnapAsync(query))
        {
            return NotFound();
        }
        string res = _snapService.GetSerializedResponse(query.MaxCoverSize);
        return Content(res,"application/json");
    }
}