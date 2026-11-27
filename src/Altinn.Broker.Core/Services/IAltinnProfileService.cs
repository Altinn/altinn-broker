namespace Altinn.Broker.Core.Services;

/// <summary>
/// Reads and updates the logged-in end user's Altinn profile.
/// Calls the platform with the user's own Altinn token, the same way Access Management does.
/// </summary>
public interface IAltinnProfileService
{
    /// <summary>
    /// The language stored on the profile (<c>nb</c>, <c>nn</c>, or <c>en</c>), or null when the profile has none.
    /// </summary>
    Task<string?> GetLanguage(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the language on the profile. <paramref name="language"/> is <c>nb</c>, <c>nn</c>, or <c>en</c>.
    /// </summary>
    Task UpdateLanguage(string language, CancellationToken cancellationToken = default);
}
