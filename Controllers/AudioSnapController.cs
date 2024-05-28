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
        AudioSnap? snap = _snapService.GetSnapByHash();
        if (snap == null)
        {
            // calculate the snap, get all the required data and save it
            snap = await _snapService.GetSnapByFingerprint(query);
            // _snapService.SaveSnap();
        }

        if (snap != null)
        {
            // custom serialize
        }

        return Ok(snap);
    }
}