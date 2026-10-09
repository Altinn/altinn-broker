namespace Altinn.Broker.Core.Services;

/// <summary>
/// The Altinn token of the end user behind the current request, for platform calls made on their behalf.
/// </summary>
public interface IEndUserTokenProvider
{
    /// <summary>Returns null when the request was not made by an authenticated end user.</summary>
    Task<string?> GetAltinnToken();
}
