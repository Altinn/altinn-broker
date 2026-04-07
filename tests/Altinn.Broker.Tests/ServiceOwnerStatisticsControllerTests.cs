using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Altinn.Broker.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Tests.Helpers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Altinn.Broker.Tests;

public class ServiceOwnerStatisticsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _serviceOwnerClient;
    private readonly HttpClient _senderClient;
    private readonly HttpClient _otherServiceOwnerClient;
    private readonly NpgsqlDataSource _dataSource;

    public ServiceOwnerStatisticsControllerTests(CustomWebApplicationFactory factory)
    {
        _serviceOwnerClient = factory.CreateClientWithAuthorization(TestConstants.DUMMY_SERVICE_OWNER_TOKEN);
        _senderClient = factory.CreateClientWithAuthorization(TestConstants.DUMMY_SENDER_TOKEN);
        _otherServiceOwnerClient = factory.CreateClientWithAuthorization(TestConstants.DUMMY_SERVICE_OWNER_TOKEN_NOT_CONFIGURED);
        _dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
    }

    [Fact]
    public async Task DownloadMonthlyStatisticsCsv_ReturnsCsvFileForCallingServiceOwner()
    {
        var serviceOwnerId = "0192:991825827";
        var resourceId = $"monthly-api-{Guid.NewGuid():N}";
        var senderId = "0192:991825827";

        await EnsureResource(resourceId, "991825827", serviceOwnerId);
        var januaryTransfer = await InsertFileTransfer(resourceId, serviceOwnerId, new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero), senderId);
        var januaryTransfer2 = await InsertFileTransfer(resourceId, serviceOwnerId, new DateTimeOffset(2026, 1, 12, 8, 0, 0, TimeSpan.Zero), senderId);
        await InsertFileTransfer(resourceId, serviceOwnerId, new DateTimeOffset(2026, 2, 10, 8, 0, 0, TimeSpan.Zero), senderId);

        await InsertActorStatus(januaryTransfer, "0192:986252932", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(januaryTransfer, "0192:986252932", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 10, 11, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(januaryTransfer, "0192:986252933", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 13, 10, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(januaryTransfer2, "0192:986252932", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 14, 10, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(januaryTransfer, "0192:986252932", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 11, 9, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(januaryTransfer, "0192:986252933", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 14, 9, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(januaryTransfer2, "0192:986252932", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero));

        var response = await _serviceOwnerClient.GetAsync(
            $"broker/api/v1/statistics/monthly?resourceId={resourceId}&year=2026&month=1");

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains("monthly_statistics_", response.Content.Headers.ContentDisposition?.FileName);

        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("year,month,resourceId,sender,recipient,uploadCount,downloadStartedCount,uniqueDownloadStartedCount,downloadConfirmedCount", csv);
        Assert.Contains($"2026,1,{resourceId},{senderId},0192:986252932,2,3,2,2", csv);
        Assert.Contains($"2026,1,{resourceId},{senderId},0192:986252933,1,1,1,1", csv);
        Assert.DoesNotContain("2026,2,", csv);
    }

    [Fact]
    public async Task DownloadMonthlyStatisticsCsv_WithGroupByPropertyKeys_AddsPropertyColumnsAtEndAndSplitsRows()
    {
        var serviceOwnerId = "0192:991825827";
        var resourceId = $"monthly-api-grouped-{Guid.NewGuid():N}";
        var senderId = "0192:991825827";
        var recipientId = "0192:986252932";

        await EnsureResource(resourceId, "991825827", serviceOwnerId);
        var transferA = await InsertFileTransfer(resourceId, serviceOwnerId, new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero), senderId);
        var transferB = await InsertFileTransfer(resourceId, serviceOwnerId, new DateTimeOffset(2026, 1, 11, 8, 0, 0, TimeSpan.Zero), senderId);

        await InsertProperty(transferA, "messageType", "invoice");
        await InsertProperty(transferA, "statusMessage", "accepted");
        await InsertProperty(transferB, "messageType", "receipt");

        await InsertActorStatus(transferA, recipientId, ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(transferA, recipientId, ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 10, 11, 0, 0, TimeSpan.Zero));
        await InsertActorStatus(transferB, recipientId, ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 11, 10, 0, 0, TimeSpan.Zero));

        var response = await _serviceOwnerClient.GetAsync(
            $"broker/api/v1/statistics/monthly?resourceId={resourceId}&year=2026&month=1&groupByPropertyKeys=messageType&groupByPropertyKeys=statusMessage");

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("year,month,resourceId,sender,recipient,uploadCount,downloadStartedCount,uniqueDownloadStartedCount,downloadConfirmedCount,messageType,statusMessage", csv);
        Assert.Contains($"2026,1,{resourceId},{senderId},{recipientId},1,1,1,1,invoice,accepted", csv);
        Assert.Contains($"2026,1,{resourceId},{senderId},{recipientId},1,1,1,0,receipt,", csv);
    }

    [Fact]
    public async Task DownloadMonthlyStatisticsCsv_InvalidMonthRange_ReturnsBadRequest()
    {
        var response = await _serviceOwnerClient.GetAsync(
            "broker/api/v1/statistics/monthly?year=2026&month=13");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var parsedError = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(parsedError);
        Assert.Equal(StatisticsErrors.InvalidMonthFormat.Message, parsedError.Detail);
    }

    [Fact]
    public async Task DownloadMonthlyStatisticsCsv_ForSenderToken_ReturnsForbidden()
    {
        var response = await _senderClient.GetAsync(
            "broker/api/v1/statistics/monthly?year=2026&month=1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DownloadMonthlyStatisticsCsv_ForResourceOwnedByAnotherServiceOwner_ReturnsUnauthorized()
    {
        var resourceId = $"monthly-api-other-{Guid.NewGuid():N}";
        await EnsureResource(resourceId, "991825827", "0192:991825827");

        var response = await _otherServiceOwnerClient.GetAsync(
            $"broker/api/v1/statistics/monthly?resourceId={resourceId}&year=2026&month=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var parsedError = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(parsedError);
        Assert.Equal(Errors.NoAccessToResource.Message, parsedError.Detail);
    }

    private async Task EnsureResource(string resourceId, string organizationNumber, string serviceOwnerId)
    {
        await EnsureStorageProvider(serviceOwnerId);

        await using var command = _dataSource.CreateCommand(
            @"INSERT INTO broker.altinn_resource (resource_id_pk, created, organization_number, service_owner_id_fk)
              VALUES (@resourceId, NOW(), @organizationNumber, @serviceOwnerId)
              ON CONFLICT (resource_id_pk) DO NOTHING");

        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@organizationNumber", organizationNumber);
        command.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> InsertFileTransfer(string resourceId, string serviceOwnerId, DateTimeOffset created, string senderExternalId = "0192:991825827")
    {
        var fileTransferId = Guid.NewGuid();
        var senderActorId = await EnsureActor(senderExternalId);
        var storageProviderId = await EnsureStorageProvider(serviceOwnerId);

        await using var command = _dataSource.CreateCommand(
            @"INSERT INTO broker.file_transfer (
                    file_transfer_id_pk, resource_id, created, filename, checksum, file_transfer_size,
                    sender_actor_id_fk, external_file_transfer_reference, expiration_time, storage_provider_id_fk, use_virus_scan)
              VALUES (
                    @fileTransferId, @resourceId, @created, @fileName, NULL, 0,
                    @senderActorId, @externalReference, @expirationTime, @storageProviderId, false)");

        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@created", created.UtcDateTime);
        command.Parameters.AddWithValue("@fileName", "stats.txt");
        command.Parameters.AddWithValue("@senderActorId", senderActorId);
        command.Parameters.AddWithValue("@externalReference", $"ref-{fileTransferId}");
        command.Parameters.AddWithValue("@expirationTime", created.UtcDateTime.AddDays(1));
        command.Parameters.AddWithValue("@storageProviderId", storageProviderId);
        await command.ExecuteNonQueryAsync();

        return fileTransferId;
    }

    private async Task InsertActorStatus(Guid fileTransferId, string actorExternalId, ActorFileTransferStatus status, DateTimeOffset statusDate)
    {
        var actorId = await EnsureActor(actorExternalId);

        await using var command = _dataSource.CreateCommand(
            @"INSERT INTO broker.actor_file_transfer_status (
                    actor_id_fk, file_transfer_id_fk, actor_file_transfer_status_description_id_fk, actor_file_transfer_status_date)
              VALUES (@actorId, @fileTransferId, @status, @statusDate)");

        command.Parameters.AddWithValue("@actorId", actorId);
        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@status", (int)status);
        command.Parameters.AddWithValue("@statusDate", statusDate.UtcDateTime);
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertProperty(Guid fileTransferId, string key, string value)
    {
        await using var command = _dataSource.CreateCommand(
            @"INSERT INTO broker.file_transfer_property (file_transfer_id_fk, key, value)
              VALUES (@fileTransferId, @key, @value)");

        command.Parameters.AddWithValue("@fileTransferId", fileTransferId);
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> EnsureActor(string actorExternalId)
    {
        await using var selectCommand = _dataSource.CreateCommand(
            "SELECT actor_id_pk FROM broker.actor WHERE actor_external_id = @actorExternalId");
        selectCommand.Parameters.AddWithValue("@actorExternalId", actorExternalId);
        var existingActorId = await selectCommand.ExecuteScalarAsync();
        if (existingActorId is long actorId)
        {
            return actorId;
        }

        await using var insertCommand = _dataSource.CreateCommand(
            "INSERT INTO broker.actor (actor_external_id) VALUES (@actorExternalId) RETURNING actor_id_pk");
        insertCommand.Parameters.AddWithValue("@actorExternalId", actorExternalId);
        return (long)(await insertCommand.ExecuteScalarAsync())!;
    }

    private async Task<long> EnsureStorageProvider(string serviceOwnerId)
    {
        await using var selectCommand = _dataSource.CreateCommand(
            "SELECT storage_provider_id_pk FROM broker.storage_provider WHERE service_owner_id_fk = @serviceOwnerId LIMIT 1");
        selectCommand.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);
        var existingStorageProviderId = await selectCommand.ExecuteScalarAsync();
        if (existingStorageProviderId is long storageProviderId)
        {
            return storageProviderId;
        }

        await using var insertServiceOwnerCommand = _dataSource.CreateCommand(
            @"INSERT INTO broker.service_owner (service_owner_id_pk, service_owner_name)
              VALUES (@serviceOwnerId, @serviceOwnerName)
              ON CONFLICT DO NOTHING");
        insertServiceOwnerCommand.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);
        insertServiceOwnerCommand.Parameters.AddWithValue("@serviceOwnerName", $"Service owner {serviceOwnerId}");
        await insertServiceOwnerCommand.ExecuteNonQueryAsync();

        await using var insertStorageProviderCommand = _dataSource.CreateCommand(
            @"INSERT INTO broker.storage_provider (
                    service_owner_id_fk, created, storage_provider_type, resource_name, active)
              VALUES (@serviceOwnerId, NOW(), @storageProviderType, @resourceName, true)
              RETURNING storage_provider_id_pk");
        insertStorageProviderCommand.Parameters.AddWithValue("@serviceOwnerId", serviceOwnerId);
        insertStorageProviderCommand.Parameters.AddWithValue("@storageProviderType", "Azurite");
        insertStorageProviderCommand.Parameters.AddWithValue("@resourceName", $"stats-storage-{Guid.NewGuid():N}");

        return (long)(await insertStorageProviderCommand.ExecuteScalarAsync())!;
    }
}
