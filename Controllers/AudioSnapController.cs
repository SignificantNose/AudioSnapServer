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
        // AudioSnap? snap = await _snapService.GetSnapByFingerprint(query);
        _snapService.SetNeededComponents(query.ReleaseProperties);
        await _snapService.CalculateSnap(query);
        string res = _snapService.GetSerializedResponse();
        
        
        return Content(res,"application/json");
        // return Ok(snap);
    }
}