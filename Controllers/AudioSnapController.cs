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
    public IActionResult SnapFingerprint(AudioSnapRequest request)
    {
        AudioSnap? snap = _snapService.GetSnapByHash();
        if (snap == null)
        {
            // calculate the snap, get all the required data and save it
            _snapService.SaveSnap();
        }

        return Ok(snap);
    }
}

// TODO: reference the request in chromalib once it appears
public class AudioSnapRequest
{
}