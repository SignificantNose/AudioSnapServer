namespace AudioSnapServer.Services.AudioSnap;
using AudioSnapServer.Models;

public interface IAudioSnapService
{
    void SetNeededComponents(IEnumerable<string> searchParameters);
    Task<bool> CalculateSnap(AudioSnapClientQuery query);
    string GetSerializedResponse();

}