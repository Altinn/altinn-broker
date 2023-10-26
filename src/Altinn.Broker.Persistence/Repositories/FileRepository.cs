using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

using Npgsql;

using File = Altinn.Broker.Core.Domain.File;

namespace Altinn.Broker.Persistence.Repositories;

public class FileRepository : IFileRepository
{
    private DatabaseConnectionProvider _connectionProvider;

    public FileRepository(DatabaseConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<File?> GetFileAsync(Guid fileId)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        using var command = new NpgsqlCommand(
            "SELECT *, sr.file_location, afs.actor_id_fk_pk, a.actor_external_id, afs.actor_file_status_id_fk, afs.actor_file_status_date FROM broker.file " +
            "LEFT JOIN broker.storage_reference sr on sr.storage_reference_id_pk = storage_reference_id_fk " +
            "LEFT JOIN broker.actor_file_status afs on afs.file_id_fk_pk = file_id_pk " +
            "LEFT JOIN broker.actor a on a.actor_id_pk = afs.actor_id_fk_pk " +
            "WHERE file_id_pk = @fileId",
            connection);

        command.Parameters.AddWithValue("@fileId", fileId);

        using NpgsqlDataReader reader = command.ExecuteReader();


        if (reader.Read())
        {
            var file = new File
            {
                FileId = reader.GetGuid(reader.GetOrdinal("file_id_pk")),
                ExternalFileReference = reader.GetString(reader.GetOrdinal("external_file_reference")),
                ShipmentId = reader.GetGuid(reader.GetOrdinal("shipment_id_fk")),
                FileStatus = (FileStatus)reader.GetInt32(reader.GetOrdinal("file_status_id_fk")),
                LastStatusUpdate = reader.GetDateTime(reader.GetOrdinal("last_status_update")),
                Uploaded = reader.GetDateTime(reader.GetOrdinal("uploaded")),
                FileLocation = reader.GetString(reader.GetOrdinal("file_location"))
            };
            if (!reader.IsDBNull(reader.GetOrdinal("actor_id_fk_pk")))
            {
                var receipts = new List<FileReceipt>();
                do
                {
                    receipts.Add(new FileReceipt()
                    {
                        FileId = fileId,
                        Actor = new Actor()
                        {
                            ActorId = reader.GetInt64(reader.GetOrdinal("actor_id_fk_pk")),
                            ActorExternalId = reader.GetString(reader.GetOrdinal("actor_external_id"))
                        },
                        Status = (ActorFileStatus)reader.GetInt32(reader.GetOrdinal("actor_file_status_id_fk")),
                        Date = reader.GetDateTime(reader.GetOrdinal("actor_file_status_date"))
                    });
                } while (reader.Read());
                file.Receipts = receipts;
            }
            return file;
        }
        else
        {
            return null;
        }
    }

    public async Task AddReceiptAsync(FileReceipt receipt)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        using (var command = new NpgsqlCommand(
            "INSERT INTO broker.actor_file_status (actor_id_fk_pk, file_id_fk_pk, actor_file_status_id_fk, actor_file_status_date) " +
            "VALUES (@actorId, @fileId, @actorFileStatusId, NOW())", connection))
        {
            command.Parameters.AddWithValue("@actorId", receipt.Actor.ActorId);
            command.Parameters.AddWithValue("@fileId", receipt.FileId);
            command.Parameters.AddWithValue("@actorFileStatusId", (int)receipt.Status);
            command.ExecuteNonQuery();
        }
    }

    public async Task AddFileAsync(File file)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        NpgsqlCommand command = new NpgsqlCommand(
            "INSERT INTO broker.file (file_id_pk, external_file_reference, shipment_id_fk, file_status_id_fk, last_status_update, uploaded, storage_reference_id_fk) " +
            "VALUES (@fileId, @externalFileReference, @shipmentId, @fileStatusId, @lastStatusUpdate, @uploaded, @storageReferenceId)",
            connection);
        long storageReference = await AddFileStorageReferenceAsync(file.FileLocation);

        command.Parameters.AddWithValue("@fileId", file.FileId);
        command.Parameters.AddWithValue("@externalFileReference", file.ExternalFileReference);
        command.Parameters.AddWithValue("@shipmentId", file.ShipmentId);
        command.Parameters.AddWithValue("@fileStatusId", (int)file.FileStatus);
        command.Parameters.AddWithValue("@lastStatusUpdate", file.LastStatusUpdate);
        command.Parameters.AddWithValue("@uploaded", file.Uploaded);
        command.Parameters.AddWithValue("@storageReferenceId", storageReference);

        command.ExecuteNonQuery();
    }

    private async Task<long> AddFileStorageReferenceAsync(string fileLocation)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        long? storageReferenceId;
        using (var command = new NpgsqlCommand(
            "INSERT INTO broker.storage_reference (storage_reference_id_pk, file_location) " +
            "VALUES (DEFAULT, @fileLocation) " +
            "RETURNING storage_reference_id_pk", connection))
        {
            command.Parameters.AddWithValue("@fileLocation", fileLocation);
            storageReferenceId = (long)command.ExecuteScalar();
            return (long)storageReferenceId;
        }
    }
}