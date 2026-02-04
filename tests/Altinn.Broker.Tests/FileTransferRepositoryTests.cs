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

	public FileTransferRepositoryTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;
		_repository = factory.Services.GetRequiredService<IFileTransferRepository>();
		_dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
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
		var fileTransferId = Guid.NewGuid();
		var senderExternalId = "0192:991825827";
		var createdValue = created ?? DateTimeOffset.UtcNow;

		// Get or create actor
		await using var getActorCommand = _dataSource.CreateCommand(
			"SELECT actor_id_pk FROM broker.actor WHERE actor_external_id = @externalId");
		getActorCommand.Parameters.AddWithValue("@externalId", senderExternalId);
		var actorIdObj = await getActorCommand.ExecuteScalarAsync();

		long actorId;
		if (actorIdObj == null)
		{
			await using var insertActorCommand = _dataSource.CreateCommand(
				"INSERT INTO broker.actor (actor_external_id) VALUES (@externalId) RETURNING actor_id_pk");
			insertActorCommand.Parameters.AddWithValue("@externalId", senderExternalId);
			actorId = (long)(await insertActorCommand.ExecuteScalarAsync())!;
		}
		else
		{
			actorId = (long)actorIdObj;
		}

		// Get storage provider (assuming one exists from test setup)
		await using var getStorageCommand = _dataSource.CreateCommand(
			"SELECT storage_provider_id_pk FROM broker.storage_provider LIMIT 1");
		var storageProviderIdObj = await getStorageCommand.ExecuteScalarAsync();

		long storageProviderId;
		if (storageProviderIdObj == null)
		{
			// Create a service owner and storage provider if they don't exist
			await using var insertServiceOwnerCommand = _dataSource.CreateCommand(
				"INSERT INTO broker.service_owner (service_owner_id_pk, service_owner_name) VALUES (@id, @name) ON CONFLICT DO NOTHING");
			insertServiceOwnerCommand.Parameters.AddWithValue("@id", "0192:991825827");
			insertServiceOwnerCommand.Parameters.AddWithValue("@name", "Test Service Owner");
			await insertServiceOwnerCommand.ExecuteNonQueryAsync();

			await using var insertStorageCommand = _dataSource.CreateCommand(
				"INSERT INTO broker.storage_provider (service_owner_id_fk, created, storage_provider_type, resource_name, active) VALUES (@serviceOwnerId, NOW(), @type, @resourceName, true) RETURNING storage_provider_id_pk");
			insertStorageCommand.Parameters.AddWithValue("@serviceOwnerId", "0192:991825827");
			insertStorageCommand.Parameters.AddWithValue("@type", "Azurite");
			insertStorageCommand.Parameters.AddWithValue("@resourceName", "test-storage");
			storageProviderId = (long)(await insertStorageCommand.ExecuteScalarAsync())!;
		}
		else
		{
			storageProviderId = (long)storageProviderIdObj;
		}

		// Create file transfer
		await using var insertFileTransferCommand = _dataSource.CreateCommand(
			"INSERT INTO broker.file_transfer (file_transfer_id_pk, resource_id, filename, checksum, file_transfer_size, external_file_transfer_reference, sender_actor_id_fk, created, storage_provider_id_fk, expiration_time, hangfire_job_id, use_virus_scan) " +
			"VALUES (@fileTransferId, @resourceId, @fileName, NULL, NULL, @externalRef, @actorId, @created, @storageProviderId, @expirationTime, NULL, false)");
		insertFileTransferCommand.Parameters.AddWithValue("@fileTransferId", fileTransferId);
		insertFileTransferCommand.Parameters.AddWithValue("@resourceId", resourceId);
		insertFileTransferCommand.Parameters.AddWithValue("@fileName", "test.txt");
		insertFileTransferCommand.Parameters.AddWithValue("@externalRef", "test-ref");
		insertFileTransferCommand.Parameters.AddWithValue("@actorId", actorId);
		insertFileTransferCommand.Parameters.AddWithValue("@created", createdValue);
		insertFileTransferCommand.Parameters.AddWithValue("@storageProviderId", storageProviderId);
		insertFileTransferCommand.Parameters.AddWithValue("@expirationTime", createdValue.UtcDateTime.AddHours(1));
		await insertFileTransferCommand.ExecuteNonQueryAsync();

		return fileTransferId;
	}

	private async Task<Guid> InsertFileTransferWithProperty(string resourceId, string propertyKey, string propertyValue)
	{
		var fileTransferId = await InsertFileTransfer(resourceId);

		// Insert property
		await using var insertPropertyCommand = _dataSource.CreateCommand(
			"INSERT INTO broker.file_transfer_property (file_transfer_id_fk, key, value) VALUES (@fileTransferId, @key, @value)");
		insertPropertyCommand.Parameters.AddWithValue("@fileTransferId", fileTransferId);
		insertPropertyCommand.Parameters.AddWithValue("@key", propertyKey);
		insertPropertyCommand.Parameters.AddWithValue("@value", propertyValue);
		await insertPropertyCommand.ExecuteNonQueryAsync();

		return fileTransferId;
	}
}

