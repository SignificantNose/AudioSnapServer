namespace AudioSnapServer.Services.AudioSnap;
using AudioSnapServer.Models;

public interface IAudioSnapService
{
    /// <summary>
    /// Analyzes the needed components list and distinguishes which
    /// of the properties current implementation of the service can
    /// retrieve, preparing to work with <see cref="CalculateSnapAsync"/>
    /// </summary>
    void SetNeededComponents(AudioSnapClientQuery query);
    
    /// <summary>
    /// Query the database and different APIs based on the
    /// needed components set in the bit-based value,
    /// calculated in <see cref="SetNeededComponents"/>
    /// </summary>
    /// <returns>
    /// True on success (<see cref="GetSerializedResponse"/> can be called succesfully),
    /// false on error (the fingerprint couldn't have been recognized)
    /// </returns>
    Task<bool> CalculateSnapAsync(AudioSnapClientQuery query);
    
    /// <summary>
    /// Forms a json-serialized string to send to the user as a response.
    /// Must be called after <see cref="CalculateSnapAsync"/> method.
    /// </summary>
    /// <param name="maxImageSize">
    /// Maximum supported image size (used only if image link is required)
    /// </param>
    string GetSerializedResponse(int? maxImageSize);

}