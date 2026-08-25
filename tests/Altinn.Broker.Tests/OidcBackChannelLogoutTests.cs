using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

using Altinn.Broker.API.IdPortenDirectAuth;
using Altinn.Broker.API.IdPortenDirectAuth.Options;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Xunit;

namespace Altinn.Broker.Tests;

public class OidcBackChannelLogoutTests
{
    private const string Issuer = "https://test.idporten.no";
    private const string ClientId = "broker-test-client";

    [Fact]
    public async Task ValidateAsync_WithValidLogoutToken_ReturnsSidAndSub()
    {
        using var rsa = RSA.Create(2048);
        var token = CreateLogoutToken(rsa, sid: "session-1", sub: "user-1", jti: "jti-1");
        var validator = CreateValidator(rsa);

        var claims = await validator.ValidateAsync(token);

        Assert.NotNull(claims);
        Assert.Equal("session-1", claims.Sid);
        Assert.Equal("user-1", claims.Sub);
        Assert.Equal("jti-1", claims.Jti);
    }

    [Fact]
    public async Task ValidateAsync_WhenNoncePresent_ReturnsNull()
    {
        using var rsa = RSA.Create(2048);
        var token = CreateLogoutToken(rsa, sid: "session-1", sub: "user-1", jti: "jti-2", extraClaims: [new Claim("nonce", "abc")]);
        var validator = CreateValidator(rsa);

        var claims = await validator.ValidateAsync(token);

        Assert.Null(claims);
    }

    [Fact]
    public async Task ValidateAsync_WhenEventMissing_ReturnsNull()
    {
        using var rsa = RSA.Create(2048);
        var token = CreateLogoutToken(rsa, sid: "session-1", sub: "user-1", jti: "jti-3", includeEvent: false);
        var validator = CreateValidator(rsa);

        var claims = await validator.ValidateAsync(token);

        Assert.Null(claims);
    }

    [Fact]
    public async Task SessionStore_RevokeBySid_IsDetected()
    {
        var store = CreateStore();

        await store.RevokeAsync("sid-a", null, TimeSpan.FromMinutes(5));

        Assert.True(await store.IsRevokedAsync("sid-a", "other-sub"));
        Assert.False(await store.IsRevokedAsync("sid-b", "other-sub"));
    }

    [Fact]
    public async Task SessionStore_DuplicateJti_IsRejected()
    {
        var store = CreateStore();

        Assert.True(await store.TryConsumeJtiAsync("jti-dup", TimeSpan.FromMinutes(5)));
        Assert.False(await store.TryConsumeJtiAsync("jti-dup", TimeSpan.FromMinutes(5)));
    }

    private static OidcLogoutTokenValidator CreateValidator(RSA rsa)
    {
        var key = new RsaSecurityKey(rsa) { KeyId = "test-key" };
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = ClientId,
            ValidateLifetime = true,
            RequireExpirationTime = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = [key],
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "sub"
        };

        return new OidcLogoutTokenValidator(
            new IdPortenDirectAuthSettings { ClientId = ClientId, Authority = Issuer },
            parameters,
            NullLogger<OidcLogoutTokenValidator>.Instance);
    }

    private static string CreateLogoutToken(
        RSA rsa,
        string sid,
        string sub,
        string jti,
        bool includeEvent = true,
        Claim[]? extraClaims = null)
    {
        var key = new RsaSecurityKey(rsa) { KeyId = "test-key" };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var claims = new List<Claim>
        {
            new("sub", sub),
            new("sid", sid),
            new(JwtRegisteredClaimNames.Jti, jti)
        };
        if (extraClaims is not null)
        {
            claims.AddRange(extraClaims);
        }

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var token = handler.CreateJwtSecurityToken(
            issuer: Issuer,
            audience: ClientId,
            subject: new ClaimsIdentity(claims),
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            issuedAt: DateTime.UtcNow,
            signingCredentials: credentials);

        if (includeEvent)
        {
            token.Payload["events"] = JsonSerializer.Deserialize<JsonElement>(
                """{"http://schemas.openid.net/event/backchannel-logout":{}}""");
        }

        return handler.WriteToken(token);
    }

    private static OidcBackChannelLogoutSessionStore CreateStore()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return new OidcBackChannelLogoutSessionStore(cache);
    }
}
