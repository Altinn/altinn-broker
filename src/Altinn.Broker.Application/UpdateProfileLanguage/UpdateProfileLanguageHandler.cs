using System.Security.Claims;

using Altinn.Broker.Core.Application;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Logging;

using OneOf;

namespace Altinn.Broker.Application.UpdateProfileLanguage;

public sealed record UpdateProfileLanguageRequest(string? Language);

/// <summary>
/// Stores the chosen language on the Altinn profile. The caller writes the persistent cookie only after this succeeds.
/// </summary>
public class UpdateProfileLanguageHandler(
    IAltinnProfileService profileService,
    ILogger<UpdateProfileLanguageHandler> logger) : IHandler<UpdateProfileLanguageRequest, string>
{
    public async Task<OneOf<string, Error>> Process(
        UpdateProfileLanguageRequest request,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        if (!ProfileLanguage.TryNormalize(request.Language, out var language))
        {
            return Errors.InvalidProfileLanguage;
        }

        try
        {
            await profileService.UpdateLanguage(language, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            logger.LogError(exception, "Could not update the language on the Altinn profile");
            return Errors.ProfileLanguageUnavailable;
        }

        return language;
    }
}
