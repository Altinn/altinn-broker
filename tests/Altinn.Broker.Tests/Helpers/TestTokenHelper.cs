using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Altinn.Broker.Common.Helpers.Models;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Broker.Tests.Helpers;

public static class TestTokenHelper
{
    public static string CreateMaskinportenToken(string organizationNumber, string scope)
    {
        var authorizationDetails = new[]
        {
            new SystemUserAuthorizationDetails
            {
                Type = "urn:altinn:systemuser",
                SystemUserId = new List<string> { "system-user-id" },
                SystemUserOrg = new SystemUserOrg
                {
                    Authority = "iso6523-actorid-upis",
                    ID = organizationNumber
                },
                SystemId = "test-system"
            }
        };

        var claims = new[]
        {
            new Claim("scope", scope),
            new Claim("client_id", "test-client"),
            new Claim("authorization_details", JsonSerializer.Serialize(authorizationDetails[0])),
            new Claim("iss", "https://test.maskinporten.no/"),
            new Claim("aud", "altinn-broker"),
            new Claim("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString()),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new Claim("jti", Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "https://test.maskinporten.no/",
            audience: "altinn-broker",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("test-key-that-is-long-enough-for-hmac")), SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static ClaimsPrincipal CreateMaskinportenUser(string organizationNumber, string scope = "altinn:broker.write")
    {
        var authorizationDetails = new SystemUserAuthorizationDetails
        {
            Type = "urn:altinn:systemuser",
            SystemUserId = new List<string> { "system-user-id" },
            SystemUserOrg = new SystemUserOrg
            {
                Authority = "iso6523-actorid-upis",
                ID = organizationNumber
            },
            SystemId = "test-system"
        };

        var claims = new[]
        {
            new Claim("scope", scope),
            new Claim("authorization_details", JsonSerializer.Serialize(authorizationDetails)),
            new Claim("client_id", "test-client"),
            new Claim("iss", "https://test.maskinporten.no/")
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    public static ClaimsPrincipal CreateAltinnUser(string organizationNumber)
    {
        var claims = new[]
        {
            new Claim("urn:altinn:orgNumber", $"0192:{organizationNumber}"),
            new Claim("scope", "altinn:broker.write"),
            new Claim("iss", "https://platform.tt02.altinn.no/")
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
} 