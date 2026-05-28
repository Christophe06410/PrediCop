namespace PrediCop.Mobile.Services;

/// <summary>
/// Plays the system notification sound to alert the user (e.g. when a mission is proposed).
/// Implemented per-platform; falls back silently if the platform cannot play sounds.
/// </summary>
public interface IAlertSoundService
{
    void PlayAlert();
}
