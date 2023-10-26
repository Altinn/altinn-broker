using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;
using Altinn.Broker.Persistence;

using Npgsql;

public class ShipmentRepository : IShipmentRepository
{
    private const string SHIPMENT_SQL = "SELECT *, ass.actor_id_fk_pk, a.actor_external_id, ass.actor_shipment_status_id_fk, ass.actor_shipment_status_date FROM broker.shipment " +
            "LEFT JOIN broker.actor_shipment_status ass on ass.shipment_id_fk_pk = shipment_id_pk " +
            "LEFT JOIN broker.actor a on a.actor_id_pk = ass.actor_id_fk_pk";

    private DatabaseConnectionProvider _connectionProvider;

    public ShipmentRepository(DatabaseConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<List<Shipment>> GetAllShipmentsAsync()
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        using var command = new NpgsqlCommand(
            SHIPMENT_SQL,
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

    public async Task<Shipment?> GetShipmentAsync(Guid shipmentId)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        using var command = new NpgsqlCommand(
            SHIPMENT_SQL +
            " WHERE shipment_id_pk = @shipmentId",
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
            if (!reader.IsDBNull(reader.GetOrdinal("actor_id_fk_pk")))
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

    public async Task AddShipmentAsync(Shipment shipment)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        NpgsqlCommand command = new NpgsqlCommand(
                "INSERT INTO broker.shipment (shipment_id_pk, external_shipment_reference, uploader_actor_id_fk, initiated, shipment_status_id_fk) " +
                "VALUES (@shipmentId, @externalShipmentReference, @uploaderActorId, @initiated, @shipmentStatusId)",
                connection);

        command.Parameters.AddWithValue("@shipmentId", shipment.ShipmentId);
        command.Parameters.AddWithValue("@externalShipmentReference", shipment.ExternalShipmentReference);
        command.Parameters.AddWithValue("@uploaderActorId", shipment.UploaderActorId);
        command.Parameters.AddWithValue("@initiated", shipment.Initiated);
        command.Parameters.AddWithValue("@shipmentStatusId", (int)shipment.ShipmentStatus);

        command.ExecuteNonQuery();
    }
}