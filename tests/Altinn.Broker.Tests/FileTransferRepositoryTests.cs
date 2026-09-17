using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Tests.Helpers;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace Altinn.Broker.Tests;

public class FileTransferRepositoryTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;
	private readonly IFileTransferRepository _repository;
	private readonly NpgsqlDataSource _dataSource;
	private readonly TestDataHelper _dataHelper;

	public FileTransferRepositoryTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;
		_repository = factory.Services.GetRequiredService<IFileTransferRepository>();
		_dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
		_dataHelper = new TestDataHelper(_dataSource);
	}

	[Fact]
	public async Task GetFileTransfersByResourceId_ReturnsOnlyTransfersOlderThanMinAgeForResource()
	{
		// Arrange
		var resourceId = $"{TestConstants.RESOURCE_FOR_TEST}-{Guid.NewGuid()}";
		var otherResourceId = $"different-resource-{Guid.NewGuid()}";

		var now = DateTimeOffset.UtcNow;
		var oldCreated = now.Subtract(TimeSpan.FromDays(20));
		var newCreated = now.Subtract(TimeSpan.FromDays(1));
		var minAge = now.Subtract(TimeSpan.FromDays(10));

		var id1 = await InsertFileTransfer(resourceId, created: oldCreated);
		var id2 = await InsertFileTransfer(resourceId, created: oldCreated);
		var id3 = await InsertFileTransfer(resourceId, created: newCreated);
		var id4 = await InsertFileTransfer(otherResourceId, created: oldCreated);

		// Act
		var result = await _repository.GetFileTransfersByResourceId(resourceId, minAge, cancellationToken: default);

		// Assert
		Assert.Contains(id1, result);
		Assert.Contains(id2, result);
		Assert.DoesNotContain(id3, result); // Too new
		Assert.DoesNotContain(id4, result); // Different resourceId
	}

	[Fact]
	public async Task GetFileTransfersByResourceId_NoMatches_ReturnsEmptyList()
	{
		// Arrange
		var resourceId = $"{TestConstants.RESOURCE_FOR_TEST}-{Guid.NewGuid()}";
		var now = DateTimeOffset.UtcNow;
		var newCreated = now.Subtract(TimeSpan.FromDays(1));
		var minAge = now.Subtract(TimeSpan.FromDays(10));

		var id1 = await InsertFileTransfer(resourceId, created: newCreated);

		// Act
		var result = await _repository.GetFileTransfersByResourceId(resourceId, minAge, cancellationToken: default);

		// Assert
		Assert.Empty(result);
		Assert.Equal(1, await CountFileTransfer(id1)); // Original file still exists
	}

	[Fact]
	public async Task HardDeleteFileTransfersByIds_DeletesSpecifiedTransfers()
	{
		// Arrange
		var resourceId = $"{TestConstants.RESOURCE_FOR_TEST}-{Guid.NewGuid()}";
		var keepId = await InsertFileTransfer(resourceId);
		var deleteId1 = await InsertFileTransfer(resourceId);
		var deleteId2 = await InsertFileTransfer(resourceId);

		// Act
		var deletedCount = await _repository.HardDeleteFileTransfersByIds(new[] { deleteId1, deleteId2 }, cancellationToken: default);

		// Assert - return value
		Assert.Equal(2, deletedCount);

		// Assert - deleted rows are gone
		Assert.Equal(0, await CountFileTransfer(deleteId1));
		Assert.Equal(0, await CountFileTransfer(deleteId2));

		// Assert - other row still exists
		Assert.Equal(1, await CountFileTransfer(keepId));
	}

	[Fact]
	public async Task HardDeleteFileTransfersByIds_EmptyList_ReturnsZero()
	{
		// Arrange
		var id = await InsertFileTransfer($"{TestConstants.RESOURCE_FOR_TEST}-{Guid.NewGuid()}");

		// Act
		var deletedCount = await _repository.HardDeleteFileTransfersByIds(Array.Empty<Guid>(), cancellationToken: default);

		// Assert
		Assert.Equal(0, deletedCount);
		Assert.Equal(1, await CountFileTransfer(id));
	}

	[Fact]
	public async Task CleanupOldFilesByResourceId_OnlyDeletesOldFilesForResource()
	{
		// Arrange
		var resourceId = $"{TestConstants.RESOURCE_FOR_TEST}-{Guid.NewGuid()}";
		var otherResourceId = $"different-resource-{Guid.NewGuid()}";

		var now = DateTimeOffset.UtcNow;
		var oldCreated = now.Subtract(TimeSpan.FromDays(20));
		var newCreated = now.Subtract(TimeSpan.FromDays(1));
		var minAge = now.Subtract(TimeSpan.FromDays(10));

		var oldId1 = await InsertFileTransfer(resourceId, created: oldCreated);
		var oldId2 = await InsertFileTransfer(resourceId, created: oldCreated);
		var newId = await InsertFileTransfer(resourceId, created: newCreated);
		var otherResourceOldId = await InsertFileTransfer(otherResourceId, created: oldCreated);

		// Act - Get and delete only old files for the resource
		var fileIdsToDelete = await _repository.GetFileTransfersByResourceId(resourceId, minAge, cancellationToken: default);
		var deletedCount = await _repository.HardDeleteFileTransfersByIds(fileIdsToDelete, cancellationToken: default);

		// Assert
		Assert.Equal(2, deletedCount);
		Assert.Equal(0, await CountFileTransfer(oldId1));
		Assert.Equal(0, await CountFileTransfer(oldId2));
		Assert.Equal(1, await CountFileTransfer(newId)); // Too new, still exists
		Assert.Equal(1, await CountFileTransfer(otherResourceOldId)); // Different resourceId, still exists
	}

	[Fact]
	public async Task GetFileTransfersByResourceId_DifferentResourceId_ReturnsEmpty()
	{
		// Arrange
		var resourceId1 = $"{TestConstants.RESOURCE_FOR_TEST}-{Guid.NewGuid()}";
		var resourceId2 = $"different-resource-{Guid.NewGuid()}";
		var now = DateTimeOffset.UtcNow;
		var oldCreated = now.Subtract(TimeSpan.FromDays(20));
		var minAge = now;

		var id1 = await InsertFileTransfer(resourceId1, created: oldCreated);

		// Act - Query with different resourceId
		var result = await _repository.GetFileTransfersByResourceId(resourceId2, minAge, cancellationToken: default);

		// Assert
		Assert.Empty(result);
		Assert.Equal(1, await CountFileTransfer(id1)); // Original file still exists
	}

	[Fact]
	public async Task GetActiveFileTransferSummariesAssociatedWithActor_SenderMatch_ReturnsTransferWithAllRecipients()
	{
		// Arrange
		var resourceId = $"active-transfers-{Guid.NewGuid()}";
		var senderExternalId = NewOrgId();
		var recipient1 = NewOrgId();
		var recipient2 = NewOrgId();

		var fileTransferId = await _dataHelper.InsertFileTransfer(resourceId, senderExternalId: senderExternalId, externalReference: "my-reference");
		await _dataHelper.InsertRecipient(fileTransferId, recipient1);
		await _dataHelper.InsertRecipient(fileTransferId, recipient2);
		await _dataHelper.SetLatestFileTransferStatus(fileTransferId, FileTransferStatus.Published);
		var actor = await _dataHelper.GetOrCreateActor(senderExternalId);

		// Act
		var result = await _repository.GetActiveFileTransferSummariesAssociatedWithActor(new FrontendFileTransferSearchEntity
		{
			Actor = actor,
			ResourceIds = [resourceId],
			Statuses = [FileTransferStatus.Published]
		}, cancellationToken: default);

		// Assert
		var summary = Assert.Single(result);
		Assert.Equal(fileTransferId, summary.FileTransferId);
		Assert.Equal(resourceId, summary.ResourceId);
		Assert.Equal(senderExternalId, summary.Sender);
		Assert.Equal("my-reference", summary.SendersFileTransferReference);
		Assert.Equal(new[] { recipient1, recipient2 }.OrderBy(r => r), summary.Recipients.OrderBy(r => r));
	}

	[Fact]
	public async Task GetActiveFileTransferSummariesAssociatedWithActor_RecipientMatch_ReturnsTransfer()
	{
		// Arrange
		var resourceId = $"active-transfers-{Guid.NewGuid()}";
		var recipientExternalId = NewOrgId();

		var fileTransferId = await _dataHelper.InsertFileTransfer(resourceId);
		await _dataHelper.InsertRecipient(fileTransferId, recipientExternalId);
		await _dataHelper.SetLatestFileTransferStatus(fileTransferId, FileTransferStatus.Published);
		var actor = await _dataHelper.GetOrCreateActor(recipientExternalId);

		// Act
		var result = await _repository.GetActiveFileTransferSummariesAssociatedWithActor(new FrontendFileTransferSearchEntity
		{
			Actor = actor,
			ResourceIds = [resourceId],
			Statuses = [FileTransferStatus.Published]
		}, cancellationToken: default);

		// Assert
		var summary = Assert.Single(result);
		Assert.Equal(fileTransferId, summary.FileTransferId);
	}

	[Fact]
	public async Task GetActiveFileTransferSummariesAssociatedWithActor_ExcludesResourceIdsNotRequested()
	{
		// Arrange
		var requestedResourceId = $"active-transfers-{Guid.NewGuid()}";
		var otherResourceId = $"active-transfers-{Guid.NewGuid()}";
		var senderExternalId = NewOrgId();

		await _dataHelper.InsertFileTransfer(requestedResourceId, senderExternalId: senderExternalId);
		var otherId = await _dataHelper.InsertFileTransfer(otherResourceId, senderExternalId: senderExternalId);
		var actor = await _dataHelper.GetOrCreateActor(senderExternalId);

		// Act
		var result = await _repository.GetActiveFileTransferSummariesAssociatedWithActor(new FrontendFileTransferSearchEntity
		{
			Actor = actor,
			ResourceIds = [requestedResourceId],
		}, cancellationToken: default);

		// Assert
		Assert.DoesNotContain(result, summary => summary.FileTransferId == otherId);
	}

	[Fact]
	public async Task GetActiveFileTransferSummariesAssociatedWithActor_MultipleResourceIds_ReturnsTransfersAcrossAllOfThem()
	{
		// Arrange
		var resourceId1 = $"active-transfers-{Guid.NewGuid()}";
		var resourceId2 = $"active-transfers-{Guid.NewGuid()}";
		var senderExternalId = NewOrgId();

		var id1 = await _dataHelper.InsertFileTransfer(resourceId1, senderExternalId: senderExternalId);
		var id2 = await _dataHelper.InsertFileTransfer(resourceId2, senderExternalId: senderExternalId);
		var actor = await _dataHelper.GetOrCreateActor(senderExternalId);

		// Act
		var result = await _repository.GetActiveFileTransferSummariesAssociatedWithActor(new FrontendFileTransferSearchEntity
		{
			Actor = actor,
			ResourceIds = [resourceId1, resourceId2],
		}, cancellationToken: default);

		// Assert
		Assert.Contains(result, summary => summary.FileTransferId == id1);
		Assert.Contains(result, summary => summary.FileTransferId == id2);
	}

	[Fact]
	public async Task GetActiveFileTransferSummariesAssociatedWithActor_ExcludesNonMatchingStatus()
	{
		// Arrange
		var resourceId = $"active-transfers-{Guid.NewGuid()}";
		var senderExternalId = NewOrgId();

		var fileTransferId = await _dataHelper.InsertFileTransfer(resourceId, senderExternalId: senderExternalId);
		await _dataHelper.SetLatestFileTransferStatus(fileTransferId, FileTransferStatus.Initialized);
		var actor = await _dataHelper.GetOrCreateActor(senderExternalId);

		// Act
		var result = await _repository.GetActiveFileTransferSummariesAssociatedWithActor(new FrontendFileTransferSearchEntity
		{
			Actor = actor,
			ResourceIds = [resourceId],
			Statuses = [FileTransferStatus.Published]
		}, cancellationToken: default);

		// Assert
		Assert.DoesNotContain(result, summary => summary.FileTransferId == fileTransferId);
	}

	[Fact]
	public async Task GetActiveFileTransferSummariesAssociatedWithActor_ExcludesUnrelatedActor()
	{
		// Arrange
		var resourceId = $"active-transfers-{Guid.NewGuid()}";
		var senderExternalId = NewOrgId();
		var unrelatedExternalId = NewOrgId();

		var fileTransferId = await _dataHelper.InsertFileTransfer(resourceId, senderExternalId: senderExternalId);
		var unrelatedActor = await _dataHelper.GetOrCreateActor(unrelatedExternalId);

		// Act
		var result = await _repository.GetActiveFileTransferSummariesAssociatedWithActor(new FrontendFileTransferSearchEntity
		{
			Actor = unrelatedActor,
			ResourceIds = [resourceId],
		}, cancellationToken: default);

		// Assert
		Assert.DoesNotContain(result, summary => summary.FileTransferId == fileTransferId);
	}

	private static string NewOrgId() => $"0192:{Random.Shared.Next(100000000, 999999999)}";

	private async Task<int> CountFileTransfer(Guid fileTransferId)
	{
		await using var command = _dataSource.CreateCommand(
			"SELECT COUNT(*) FROM broker.file_transfer WHERE file_transfer_id_pk = @id");
		command.Parameters.AddWithValue("@id", fileTransferId);
		var resultObj = await command.ExecuteScalarAsync();
		return resultObj == null ? 0 : Convert.ToInt32(resultObj);
	}

	private async Task<Guid> InsertFileTransfer(string resourceId, DateTimeOffset? created = null)
	{
		return await _dataHelper.InsertFileTransfer(resourceId, created: created);
	}
}

