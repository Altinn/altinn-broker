using Npgsql;
using Altinn.Broker.Core.Domain;
using File = Altinn.Broker.Core.Domain.File;
using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Persistence.Repositories;

public class FileRepository
{
    private readonly string _connectionString;

    public FileRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public File? GetFile(Guid fileId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = new NpgsqlCommand(
            "SELECT * FROM broker.file, sr.file_location, afs.actor_id_fk_pk, a.actor_external_id, afs.actor_file_status_id_fk, afs.actor_file_status_date " +
            "INNER JOIN broker.storage_reference sr on sr.storage_reference_id_pk = storage_reference_id_fk " +
            "LEFT JOIN broker.actor_file_status afs on afs.file_id_fk_pk = file_id_pk " +
            "INNER JOIN broker.actor a on actor.actor_id_pk = afs.actor_id_fk_pk " +
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
                StorageReferenceId = reader.GetString(reader.GetOrdinal("storage_reference_id_fk")),
                FileLocation = reader.GetString(reader.GetOrdinal("file_location"))
            };
            if (reader.GetInt64(reader.GetOrdinal("actor_id_fk_pk")) > 0) { 
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

    public void AddReceipt(FileReceipt receipt)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using (var command = new NpgsqlCommand(
            "INSERT INTO broker.actor_file_status (actor_id_fk_pk, file_id_fk_pk, actor_file_status_id_fk, actor_file_status_date) " + 
            "VALUES (@actorId, @fileId, @actorFileStatusId, NOW()) " + 
            "RETURNING storage_reference_id_pk", connection))
        {
            command.Parameters.AddWithValue("@actorId", receipt.Actor.ActorId);
            command.Parameters.AddWithValue("@fileId", receipt.FileId);
            command.Parameters.AddWithValue("@actorFileStatusId", (int)receipt.Status);
            command.ExecuteNonQuery();
        }
    }

    public void SaveFile(File file)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM broker.file WHERE file_id_pk = @fileId", connection))
        {
            checkCommand.Parameters.AddWithValue("@fileId", file.FileId);
            long existingCount = (long)checkCommand.ExecuteScalar();

            NpgsqlCommand command;

            if (existingCount == 0)
            {
                command = new NpgsqlCommand(
                    "INSERT INTO broker.file (file_id_pk, external_file_reference, shipment_id_fk, file_status_id_fk, last_status_update, uploaded, storage_reference_id_fk) " +
                    "VALUES (@fileId, @externalFileReference, @shipmentId, @fileStatusId, @lastStatusUpdate, @uploaded, @storageReferenceId)",
                    connection);
            }
            else
            {
                command = new NpgsqlCommand(
                    "UPDATE broker.file " +
                    "SET external_file_reference = @externalFileReference, shipment_id_fk = @shipmentId, file_status_id_fk = @fileStatusId, last_status_update = @lastStatusUpdate, uploaded = @uploaded, storage_reference_id_fk = @storageReferenceId " +
                    "WHERE file_id_pk = @fileId",
                    connection);
            }

            command.Parameters.AddWithValue("@fileId", file.FileId);
            command.Parameters.AddWithValue("@externalFileReference", file.ExternalFileReference);
            command.Parameters.AddWithValue("@shipmentId", file.ShipmentId);
            command.Parameters.AddWithValue("@fileStatusId", (int)file.FileStatus);
            command.Parameters.AddWithValue("@lastStatusUpdate", file.LastStatusUpdate);
            command.Parameters.AddWithValue("@uploaded", file.Uploaded);
            command.Parameters.AddWithValue("@storageReferenceId", file.StorageReferenceId);

            command.ExecuteNonQuery();
        }
    }

    public void AddFileStorageReference(Guid fileId, string fileLocation)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        Guid? storageReferenceId;
        using (var command = new NpgsqlCommand(
            "INSERT INTO broker.storage_reference (storage_reference_id_pk, file_location) " + 
            "VALUES (uuid_generate_v4(), @fileLocation) " + 
            "RETURNING storage_reference_id_pk", connection))
        {
            command.Parameters.AddWithValue("@fileLocation", fileLocation);
            storageReferenceId = (Guid)command.ExecuteScalar();
        }

        using (var command = new NpgsqlCommand("UPDATE broker.file SET storage_reference_id_fk = @storageReferenceId WHERE file_id_pk = @fileId", connection))
        {
            command.Parameters.AddWithValue("@fileId", fileId);
            command.Parameters.AddWithValue("@storageReferenceId", storageReferenceId);
            command.ExecuteNonQuery();
        }
    }
}
