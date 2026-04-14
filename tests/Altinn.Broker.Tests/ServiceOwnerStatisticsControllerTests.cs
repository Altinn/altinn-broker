using System.Net;
using System.Net.Http.Json;

using Altinn.Broker.Application;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
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
    private readonly IMonthlyStatisticsRepository _repository;
    private readonly TestDataHelper _dataHelper;

    public ServiceOwnerStatisticsControllerTests(CustomWebApplicationFactory factory)
    {
        _serviceOwnerClient = factory.CreateClientWithAuthorization(TestConstants.DUMMY_SERVICE_OWNER_TOKEN);
        _senderClient = factory.CreateClientWithAuthorization(TestConstants.DUMMY_SENDER_TOKEN);
        _otherServiceOwnerClient = factory.CreateClientWithAuthorization(TestConstants.DUMMY_SERVICE_OWNER_TOKEN_NOT_CONFIGURED);
        _repository = factory.Services.GetRequiredService<IMonthlyStatisticsRepository>();
        _dataHelper = new TestDataHelper(factory.Services.GetRequiredService<NpgsqlDataSource>());
    }

    [Fact]
    public async Task DownloadMonthlyStatisticsCsv_ReturnsCsvFileForCallingServiceOwner()
    {
        var serviceOwnerId = "0192:991825827";
        var resourceId = $"monthly-api-{Guid.NewGuid():N}";
        var senderId = "0192:991825827";

        await _dataHelper.EnsureResource(resourceId, "991825827", serviceOwnerId);
        var januaryTransfer = await _dataHelper.InsertFileTransfer(resourceId, serviceOwnerId, created: new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero), senderExternalId: senderId);
        var januaryTransfer2 = await _dataHelper.InsertFileTransfer(resourceId, serviceOwnerId, created: new DateTimeOffset(2026, 1, 12, 8, 0, 0, TimeSpan.Zero), senderExternalId: senderId);
        var januaryTransferInitializedOnly = await _dataHelper.InsertFileTransfer(resourceId, serviceOwnerId, created: new DateTimeOffset(2026, 1, 16, 8, 0, 0, TimeSpan.Zero), senderExternalId: senderId);
        await _dataHelper.InsertFileTransfer(resourceId, serviceOwnerId, created: new DateTimeOffset(2026, 2, 10, 8, 0, 0, TimeSpan.Zero), senderExternalId: senderId);

        await _dataHelper.InsertActorStatus(januaryTransfer, "0192:986252932", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(januaryTransfer, "0192:986252932", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 10, 11, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(januaryTransfer, "0192:986252933", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 13, 10, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(januaryTransfer2, "0192:986252932", ActorFileTransferStatus.DownloadStarted, new DateTimeOffset(2026, 1, 14, 10, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(januaryTransferInitializedOnly, "0192:986252932", ActorFileTransferStatus.Initialized, new DateTimeOffset(2026, 1, 16, 10, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertFileTransferStatus(januaryTransfer, FileTransferStatus.Published, new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertFileTransferStatus(januaryTransfer2, FileTransferStatus.Published, new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(januaryTransfer, "0192:986252932", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 11, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(januaryTransfer, "0192:986252933", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 14, 9, 0, 0, TimeSpan.Zero));
        await _dataHelper.InsertActorStatus(januaryTransfer2, "0192:986252932", ActorFileTransferStatus.DownloadConfirmed, new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero));

        await _repository.RebuildMonthlyStatisticsRollupForMonth(2026, 1, CancellationToken.None);

        var response = await _serviceOwnerClient.GetAsync(
            $"broker/api/v1/statistics/monthly?resourceId={resourceId}&year=2026&month=1");

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains("monthly_statistics_", response.Content.Headers.ContentDisposition?.FileName);

        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("year,month,resourceId,sender,recipient,totalFileTransfers,uploadCount,totalTransferDownloadAttempts,transfersWithDownloadConfirmed", csv);
        Assert.Contains($"2026,1,{resourceId},{senderId},0192:986252932,3,2,2,2", csv);
        Assert.Contains($"2026,1,{resourceId},{senderId},0192:986252933,1,1,1,1", csv);
        Assert.DoesNotContain("2026,2,", csv);
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
    public async Task DownloadMonthlyStatisticsCsv_YearAboveDateTimeRange_ReturnsBadRequest()
    {
        var response = await _serviceOwnerClient.GetAsync(
            "broker/api/v1/statistics/monthly?year=10000&month=1");

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
        await _dataHelper.EnsureResource(resourceId, "991825827", "0192:991825827");

        var response = await _otherServiceOwnerClient.GetAsync(
            $"broker/api/v1/statistics/monthly?resourceId={resourceId}&year=2026&month=1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var parsedError = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(parsedError);
        Assert.Equal(Errors.NoAccessToResource.Message, parsedError.Detail);
    }

}
