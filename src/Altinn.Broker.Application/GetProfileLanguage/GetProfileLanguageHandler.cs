using System.Security.Claims;

using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.GetProfileLanguage;

/// <summary>
/// The language stored on the end user's Altinn profile.
/// A missing profile is a successful empty result, so the caller can fall back to the language cookie.
/// </summary>
public sealed record ProfileLanguageResult(string? Language);

public class GetProfileLanguageHandler(
    IAltinnProfileService profileService,
    ILogger<GetProfileLanguageHandler> logger) : IHandler<ProfileLanguageResult>
{
    public async Task<OneOf<ProfileLanguageResult, Error>> Process(ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        string? language;
        try
        {
            language = await profileService.GetLanguage(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            logger.LogError(exception, "Could not read the language from the Altinn profile");
            return Errors.ProfileLanguageUnavailable;
        }

        if (language is not null && ProfileLanguage.TryNormalize(language, out var normalized))
        {
            return new ProfileLanguageResult(normalized);
        }

        return new ProfileLanguageResult(null);
    }
}
