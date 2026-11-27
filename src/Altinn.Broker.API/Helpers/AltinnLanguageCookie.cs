using Altinn.Broker.API.Configuration;
using Altinn.Broker.Application;

using Microsoft.AspNetCore.Http;

namespace Altinn.Broker.API.Helpers;

/// <summary>
/// The shared Altinn language cookie. Other Altinn apps, including Arbeidsflate, read the same <c>UL</c> value.
/// </summary>
public static class AltinnLanguageCookie
{
    public const string Name = "altinnPersistentContext";

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public static string? Read(HttpRequest request)
    {
        return request.Cookies.TryGetValue(Name, out var value)
            ? ProfileLanguage.FromPersistentContext(value)
            : null;
    }

    public static void Write(HttpResponse response, HttpRequest request, string language, string? domain)
    {
        var ulCode = ProfileLanguage.ToUlCode(language);
        if (ulCode is null)
        {
            return;
        }

        var value = ProfileLanguage.MergePersistentContext(request.Cookies[Name], ulCode);
        var options = new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Secure = true,
            IsEssential = true,
            SameSite = AuthCookieDefaults.SameSite,
            Expires = DateTimeOffset.UtcNow.Add(Lifetime),
            MaxAge = Lifetime,
        };

        if (!string.IsNullOrWhiteSpace(domain))
        {
            options.Domain = domain;
        }

        response.Cookies.Append(Name, value, options);
    }
}
