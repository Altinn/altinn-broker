using System.Security.Claims;

using Altinn.Broker.API.Authentication;
using Altinn.Broker.API.Configuration;
using Altinn.Broker.API.Helpers;
using Altinn.Broker.API.IdPortenDirectAuth;
using Altinn.Common.PEP.Authorization;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Altinn.Broker.Tests;

public class IdPortenAuthorizationPipelineTests
{
    [Theory]
    [InlineData(AuthorizationConstants.Sender)]
    [InlineData(AuthorizationConstants.Recipient)]
    [InlineData(AuthorizationConstants.SenderOrRecipient)]
    public async Task EndUserBrokerPolicies_IncludeCookieAuthenticationSchemes(string policyName)
    {
        using var factory = new CustomWebApplicationFactory();
        var policyProvider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await policyProvider.GetPolicyAsync(policyName);

        Assert.NotNull(policy);
        Assert.Contains(AuthorizationConstants.EndUserCookie, policy.AuthenticationSchemes);
        Assert.Contains(AuthorizationConstants.AltinnPlatformJwtCookie, policy.AuthenticationSchemes);
    }

    [Theory]
    [InlineData(AuthorizationConstants.EndUserCookie)]
    [InlineData(AuthorizationConstants.AltinnPlatformJwtCookie)]
    public async Task EndUserScopeAccessHandler_WithAuthenticatedEndUserCookie_SucceedsScopeGate(
        string authenticationType)
    {
        var requirement = new ScopeAccessRequirement(AuthorizationConstants.SenderScope);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("urn:altinn:userid", "test-user")
        ], authenticationType));
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new ScopeAccessHandler().HandleAsync(context);
        await new EndUserScopeAccessHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task EndUserScopeAccessHandler_WithBearerIdentity_DoesNotBypassScopeGate()
    {
        var requirement = new ScopeAccessRequirement(AuthorizationConstants.SenderScope);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("urn:altinn:userid", "test-user")
        ], JwtBearerDefaults.AuthenticationScheme));
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new EndUserScopeAccessHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task EndUserScopeAccessHandler_WithoutAltinnEndUserClaim_DoesNotBypassScopeGate()
    {
        var requirement = new ScopeAccessRequirement(AuthorizationConstants.SenderScope);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("pid", "test-person")
        ], AuthorizationConstants.EndUserCookie));
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new EndUserScopeAccessHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task EndUserScopeAccessHandler_WithPrivilegedScope_DoesNotBypassScopeGate()
    {
        var requirement = new ScopeAccessRequirement(AuthorizationConstants.ServiceOwnerScope);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("urn:altinn:userid", "test-user")
        ], AuthorizationConstants.EndUserCookie));
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new EndUserScopeAccessHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public void CreateIdentity_PreservesOnlyClaimsNeededForIdportenPdpMapping()
    {
        const string issuer = "https://test.idporten.no";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("iss", issuer, ClaimValueTypes.String, issuer),
            new Claim("pid", "test-person", ClaimValueTypes.String, issuer),
            new Claim("acr", "idporten-loa-substantial", ClaimValueTypes.String, issuer),
            new Claim("email", "not-preserved", ClaimValueTypes.String, issuer)
        ], "OpenIdConnect"));

        var identity = IdPortenPrincipalClaims.CreateIdentity(principal);

        Assert.Equal(IdPortenPrincipalClaims.AuthenticationType, identity.AuthenticationType);
        Assert.Equal(3, identity.Claims.Count());
        Assert.Equal(issuer, identity.FindFirst("iss")?.Value);
        Assert.Equal("test-person", identity.FindFirst("pid")?.Value);
        Assert.Equal("idporten-loa-substantial", identity.FindFirst("acr")?.Value);
        Assert.False(identity.HasClaim(claim => claim.Type == "email"));
    }

    [Fact]
    public void CreateIdentity_WhenIssuerClaimIsMissing_UsesValidatedTokenIssuer()
    {
        const string issuer = "https://test.idporten.no";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("pid", "test-person")
        ], "OpenIdConnect"));

        var identity = IdPortenPrincipalClaims.CreateIdentity(principal, issuer);

        Assert.Equal(issuer, identity.FindFirst("iss")?.Value);
        Assert.Equal(issuer, identity.FindFirst("iss")?.Issuer);
    }

    [Theory]
    [InlineData(AuthorizationConstants.EndUserCookie)]
    [InlineData(AuthorizationConstants.AltinnPlatformJwtCookie)]
    public async Task CsrfProtection_WithCookieAuthenticatedMutationAndNoHeader_ReturnsForbidden(
        string authenticationType)
    {
        var nextCalled = false;
        var middleware = new CsrfProtectionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateMutationContext(authenticationType);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task CsrfProtection_WithBearerAuthenticatedMutation_DoesNotBlockRequest()
    {
        var nextCalled = false;
        var middleware = new CsrfProtectionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateMutationContext(JwtBearerDefaults.AuthenticationScheme);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task CsrfProtection_WithCookieAuthenticatedMutationAndHeader_DoesNotBlockRequest()
    {
        var nextCalled = false;
        var middleware = new CsrfProtectionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateMutationContext(AuthorizationConstants.EndUserCookie);
        context.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    private static DefaultHttpContext CreateMutationContext(string authenticationType)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "test-user")
        ], authenticationType));
        return context;
    }
}
