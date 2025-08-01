using System.Security.Claims;
using System.Text.Json;
using Altinn.Broker.Common;
using Altinn.Broker.Common.Helpers.Models;
using Altinn.Broker.Integrations.Altinn.Authorization;
using Altinn.Broker.Tests.Helpers;
using Xunit;
using System.Net.Http.Json;
using Altinn.Broker.Tests.Factories;
using Altinn.Broker.API.Models;

namespace Altinn.Broker.Tests;

public class MaskinportenTokenTests
{
    [Fact]
    public void GetCallerOrganizationId_WithMaskinportenToken_ReturnsCorrectOrgNumber()
    {
        // Arrange
        var user = TestTokenHelper.CreateMaskinportenUser("0192:123456789");

        // Act
        var orgId = user.GetCallerOrganizationId();

        // Assert
        Assert.Equal("123456789", orgId);
    }

    [Fact]
    public void GetCallerOrganizationId_WithMaskinportenTokenWithPrefix_ReturnsOrgNumberWithoutPrefix()
    {
        // Arrange  
        var authorizationDetails = new SystemUserAuthorizationDetails
        {
            Type = "urn:altinn:systemuser",
            SystemUserId = new List<string> { "system-user-id" },
            SystemUserOrg = new SystemUserOrg
            {
                Authority = "iso6523-actorid-upis",
                ID = "0192:123456789" // With prefix
            },
            SystemId = "test-system"
        };

        var claims = new[]
        {
            new Claim("authorization_details", JsonSerializer.Serialize(authorizationDetails))
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var orgId = user.GetCallerOrganizationId();

        // Assert
        Assert.Equal("123456789", orgId); // Should strip prefix
    }

    [Fact]
    public void GetCallerOrganizationId_WithAltinnToken_ReturnsCorrectOrgNumber()
    {
        // Arrange
        var user = TestTokenHelper.CreateAltinnUser("987654321");

        // Act
        var orgId = user.GetCallerOrganizationId();

        // Assert
        Assert.Equal("987654321", orgId);
    }

    [Fact]
    public void GetCallerOrganizationId_WithInvalidAuthorizationDetails_ReturnsNull()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("authorization_details", "invalid-json")
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        var orgId = user.GetCallerOrganizationId();

        // Assert
        Assert.Null(orgId);
    }

    [Fact]
    public void CreateSubjectCategory_WithMaskinportenToken_IncludesScopeAttribute()
    {
        // Arrange
        var user = TestTokenHelper.CreateMaskinportenUser("123456789", "altinn:broker.read");

        // Act
        var category = XacmlMappers.CreateMaskinportenSubjectCategory(user);

        // Assert
        var scopeAttr = category.Attribute.FirstOrDefault(a => a.AttributeId == "urn:scope");
        Assert.NotNull(scopeAttr);
        Assert.Equal("altinn:broker.read", scopeAttr.Value);
    }


    /// <summary>
    /// Integration tests that verify pure Maskinporten tokens are accepted without exchange to Altinn tokens
    /// </summary>
    public class MaskinportenTokenIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly JsonSerializerOptions _responseSerializerOptions;

        public MaskinportenTokenIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _responseSerializerOptions = new JsonSerializerOptions(new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            });
            _responseSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        }

        [Fact]
        public async Task FileTransfer_WithValidMaskinportenToken_SuccessfullyInitializes()
        {
            // Arrange - Create a valid Maskinporten token (not expired)
            var maskinportenToken = TestTokenHelper.CreateMaskinportenToken("991825827", "altinn:broker.write");
            var client = _factory.CreateClientWithAuthorization(maskinportenToken);

            // Act - Try to initialize a file transfer using the valid Maskinporten token
            var initializeRequest = FileTransferInitializeExtTestFactory.BasicFileTransfer();
            var response = await client.PostAsJsonAsync("broker/api/v1/filetransfer", initializeRequest);

            // Assert - The request should succeed, proving valid Maskinporten tokens are accepted
            Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

            var fileTransferResponse = await response.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>(_responseSerializerOptions);
            Assert.NotNull(fileTransferResponse);
            Assert.NotEqual(Guid.Empty, fileTransferResponse.FileTransferId);
        }

        [Fact]
        public async Task FileTransfer_WithPureMaskinportenTokenReadScope_CanGetFileTransferDetails()
        {
            // Arrange - First create a file transfer with write scope
            var writeToken = TestTokenHelper.CreateMaskinportenToken("991825827", "altinn:broker.write");
            var writeClient = _factory.CreateClientWithAuthorization(writeToken);

            var initializeRequest = FileTransferInitializeExtTestFactory.BasicFileTransfer();
            var initResponse = await writeClient.PostAsJsonAsync("broker/api/v1/filetransfer", initializeRequest);
            var initResult = await initResponse.Content.ReadFromJsonAsync<FileTransferInitializeResponseExt>(_responseSerializerOptions);

            // Act - Now try to read details with a valid read scope Maskinporten token
            var readToken = TestTokenHelper.CreateMaskinportenToken("991825827", "altinn:broker.read");
            var readClient = _factory.CreateClientWithAuthorization(readToken);

            var detailsResponse = await readClient.GetAsync($"broker/api/v1/filetransfer/{initResult.FileTransferId}");

            // Assert - Should be able to read details with read scope
            Assert.True(detailsResponse.IsSuccessStatusCode, $"Expected success but got {detailsResponse.StatusCode}: {await detailsResponse.Content.ReadAsStringAsync()}");
        }


        [Fact]
        public async Task LegacyApi_WithValidMaskinportenToken_SuccessfullyInitializes()
        {
            // Arrange - Test that valid Maskinporten tokens work with legacy API endpoints too
            var maskinportenToken = TestTokenHelper.CreateMaskinportenToken("991825827", "altinn:broker.legacy");
            var client = _factory.CreateClientWithAuthorization(maskinportenToken);

            // Act - Try to initialize using legacy API with valid Maskinporten token
            var initializeRequest = FileTransferInitializeExtTestFactory.BasicFileTransfer();
            var response = await client.PostAsJsonAsync("broker/api/v1/legacy/file", initializeRequest);

            // Assert - Should work with legacy API too
            Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

            var fileId = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(fileId));
        }
    }
}