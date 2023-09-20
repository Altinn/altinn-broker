using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;

using Npgsql;

public class ShipmentRepository
{
    private readonly string _connectionString;

    public ShipmentRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public List<Shipment> GetAllShipments()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = new NpgsqlCommand(
            "SELECT * FROM broker.shipment, ass.actor_id_fk_pk, a.actor_external_id, ass.actor_shipment_status_id_fk, ass.actor_shipment_status_date " +
            "LEFT JOIN broker.actor_shipment_status ass on ass.shipment_id_fk_pk = shipment_id_pk " +
            "INNER JOIN broker.actor a on a.actor_id_pk = ass.actor_id_fk_pk",
            connection);

        using NpgsqlDataReader reader = command.ExecuteReader();

        var shipments = new List<Shipment>();
        while (reader.Read())
        {
            var shipment = new Shipment
            {
                ShipmentId = reader.GetGuid(reader.GetOrdinal("shipment_id_pk")),
                ExternalShipmentReference = reader.GetString(reader.GetOrdinal("external_shipment_reference")),
                UploaderActorId = reader.GetInt64(reader.GetOrdinal("uploader_actor_id_fk")),
                Initiated = reader.GetDateTime(reader.GetOrdinal("initiated")),
                ShipmentStatus = (ShipmentStatus)reader.GetInt32(reader.GetOrdinal("shipment_status_id_fk"))
            };
            if (reader.GetInt64(reader.GetOrdinal("actor_id_fk_pk")) > 0)
            {
                var currentShipment = reader.GetGuid(reader.GetOrdinal("shipment_id_pk"));
                var receipts = new List<ShipmentReceipt>();
                do
                {
                    receipts.Add(new ShipmentReceipt()
                    {
                        ShipmentId = currentShipment,
                        Actor = new Actor()
                        {
                            ActorId = reader.GetInt64(reader.GetOrdinal("actor_id_fk_pk")),
                            ActorExternalId = reader.GetString(reader.GetOrdinal("actor_external_id"))
                        },
                        Status = (ActorShipmentStatus)reader.GetInt32(reader.GetOrdinal("actor_shipment_status_id_fk")),
                        Date = reader.GetDateTime(reader.GetOrdinal("actor_shipment_status_date"))
                    });
                } while (reader.Read() && reader.GetGuid(reader.GetOrdinal("shipment_id_pk")) == currentShipment);
                shipment.Receipts = receipts;
            }
            shipments.Add(shipment);
        }
        return shipments;

    }

    public Shipment? GetShipment(Guid shipmentId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        using var command = new NpgsqlCommand(
            "SELECT s.* " +
            "FROM broker.shipment s " +
            "WHERE s.shipment_id_pk = @shipmentId", 
            connection);
        
        command.Parameters.AddWithValue("@shipmentId", shipmentId);
        
        using NpgsqlDataReader reader = command.ExecuteReader();

        Shipment? shipment = null;

        while (reader.Read())
        {
            shipment = new Shipment
            {
                ShipmentId = reader.GetGuid(reader.GetOrdinal("shipment_id_pk")),
                ExternalShipmentReference = reader.GetString(reader.GetOrdinal("external_shipment_reference")),
                UploaderActorId = reader.GetInt64(reader.GetOrdinal("uploader_actor_id_fk")),
                Initiated = reader.GetDateTime(reader.GetOrdinal("initiated")),
                ShipmentStatus = (ShipmentStatus)reader.GetInt32(reader.GetOrdinal("shipment_status_id_fk"))
            };
            if (reader.GetInt64(reader.GetOrdinal("actor_id_fk_pk")) > 0)
            {
                var receipts = new List<ShipmentReceipt>();
                do
                {
                    receipts.Add(new ShipmentReceipt()
                    {
                        ShipmentId = reader.GetGuid(reader.GetOrdinal("shipment_id_pk")),
                        Actor = new Actor()
                        {
                            ActorId = reader.GetInt64(reader.GetOrdinal("actor_id_fk_pk")),
                            ActorExternalId = reader.GetString(reader.GetOrdinal("actor_external_id"))
                        },
                        Status = (ActorShipmentStatus)reader.GetInt32(reader.GetOrdinal("actor_shipment_status_id_fk")),
                        Date = reader.GetDateTime(reader.GetOrdinal("actor_shipment_status_date"))
                    });
                } while (reader.Read());
                shipment.Receipts = receipts;
            }
        }

        return shipment;
    }
    
    public void SaveShipment(Shipment shipment)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        // Here's a simple method to determine if the Shipment exists based on its primary key
        using (var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM broker.shipment WHERE shipment_id_pk = @shipmentId", connection))
        {
            checkCommand.Parameters.AddWithValue("@shipmentId", shipment.ShipmentId);
            long existingCount = (long)checkCommand.ExecuteScalar();

            NpgsqlCommand command;

            if (existingCount == 0)
            {
                // Insert new Shipment
                command = new NpgsqlCommand(
                    "INSERT INTO broker.shipment (shipment_id_pk, external_shipment_reference, uploader_actor_id_fk, initiated, shipment_status_id_fk) " +
                    "VALUES (@shipmentId, @externalShipmentReference, @uploaderActorId, @initiated, @shipmentStatusId)", 
                    connection);
            }
            else
            {
                // Update existing Shipment
                command = new NpgsqlCommand(
                    "UPDATE broker.shipment " +
                    "SET external_shipment_reference = @externalShipmentReference, uploader_actor_id_fk = @uploaderActorId, initiated = @initiated, shipment_status_id_fk = @shipmentStatusId " +
                    "WHERE shipment_id_pk = @shipmentId", 
                    connection);
            }

            command.Parameters.AddWithValue("@shipmentId", shipment.ShipmentId);
            command.Parameters.AddWithValue("@externalShipmentReference", shipment.ExternalShipmentReference);
            command.Parameters.AddWithValue("@uploaderActorId", shipment.UploaderActorId);
            command.Parameters.AddWithValue("@initiated", shipment.Initiated);
            command.Parameters.AddWithValue("@shipmentStatusId", shipment.ShipmentStatusId);

            command.ExecuteNonQuery();
        }
    }
}
