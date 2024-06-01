namespace AudioSnapServer.Services.AudioSnap;
using AudioSnapServer.Models;

public interface IAudioSnapService
{
    void SetNeededComponents(AudioSnapClientQuery query);
    Task<bool> CalculateSnap(AudioSnapClientQuery query);
    string GetSerializedResponse(int? maxImageSize);

}