using Altinn.Broker.API.AltinnPlatformAuth.Options;
using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Helpers;
using Altinn.Broker.Application;
using Altinn.Broker.Application.GetProfileLanguage;
using Altinn.Broker.Application.UpdateProfileLanguage;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Altinn.Broker.Controllers;

/// <summary>
/// The end user's Altinn profile language, shared with the rest of Altinn through the profile and the persistent cookie.
/// </summary>
[ApiController]
[Route("broker/api/v1/profile")]
public class ProfileController(IOptions<AltinnPlatformAuthSettings> platformAuthSettings) : Controller
{
    public record ProfileLanguageResponse(string Language);

    public record UpdateProfileLanguageBody(string? Language);

    /// <summary>
    /// The language to show BrokerBox in.
    /// </summary>
    /// <remarks>
    /// Uses the language on the Altinn profile. When the profile has none, uses the
    /// <c>altinnPersistentContext</c> cookie, and otherwise Bokmål. A successful response refreshes that cookie.
    /// </remarks>
    /// <response code="200">The language to use</response>
    /// <response code="401">You must be logged in as an end user</response>
    /// <response code="503">The Altinn profile could not be reached</response>
    [HttpGet("language")]
    [Authorize(Policy = AuthorizationConstants.EndUser)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ProfileLanguageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ProfileLanguageResponse>> GetLanguage(
        [FromServices] GetProfileLanguageHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Process(HttpContext.User, cancellationToken);
        return result.Match(
            language => Ok(Remember(language.Language)),
            Problem);
    }

    /// <summary>
    /// Stores the chosen language on the Altinn profile and in the persistent cookie.
    /// </summary>
    /// <response code="200">The language that was stored</response>
    /// <response code="400">The language is not nb, nn, or en</response>
    /// <response code="401">You must be logged in as an end user</response>
    /// <response code="503">The Altinn profile could not be reached</response>
    [HttpPut("language")]
    [Authorize(Policy = AuthorizationConstants.EndUser)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ProfileLanguageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ProfileLanguageResponse>> UpdateLanguage(
        [FromBody] UpdateProfileLanguageBody? body,
        [FromServices] UpdateProfileLanguageHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Process(new UpdateProfileLanguageRequest(body?.Language), HttpContext.User, cancellationToken);
        return result.Match(
            language =>
            {
                AltinnLanguageCookie.Write(Response, Request, language, platformAuthSettings.Value.CookieDomain);
                return Ok(new ProfileLanguageResponse(language));
            },
            Problem);
    }

    private ProfileLanguageResponse Remember(string? profileLanguage)
    {
        if (!ProfileLanguage.TryNormalize(profileLanguage, out var language))
        {
            language = AltinnLanguageCookie.Read(Request) ?? ProfileLanguage.Bokmal;
        }

        AltinnLanguageCookie.Write(Response, Request, language, platformAuthSettings.Value.CookieDomain);
        return new ProfileLanguageResponse(language);
    }

    private ActionResult Problem(Error error) => ProblemDetailsHelper.ToProblemResult(error);
}
