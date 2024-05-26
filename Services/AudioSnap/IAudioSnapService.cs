namespace AudioSnapServer.Services.AudioSnap;
using AudioSnapServer.Models;

public interface IAudioSnapService
{
    AudioSnap? GetSnapByHash();
    Task<AudioSnap?> GetSnapByFingerprint();
    void SaveSnap();
}