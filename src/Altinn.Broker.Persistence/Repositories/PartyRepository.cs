using Altinn.Broker.Core.Domain;
using Altinn.Broker.Core.Repositories;

using Npgsql;

namespace Altinn.Broker.Persistence.Repositories;
public class PartyRepository : IPartyRepository
{
    private readonly DatabaseConnectionProvider _connectionProvider;

    public PartyRepository(DatabaseConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<PartyEntity?> GetParty(string organizationId, CancellationToken cancellationToken)
    {
        await using var command = await _connectionProvider.CreateCommand(
            "SELECT organization_number_pk, party_id, created from broker.party " +
            "WHERE organization_number = @organizationId ");
        command.Parameters.AddWithValue("@organizationId", organizationId);

        using NpgsqlDataReader reader = command.ExecuteReader();
        PartyEntity? partyData = null;
        while (reader.Read())
        {
            partyData = new PartyEntity
            {
                OrganizationNumber = reader.GetString(reader.GetOrdinal("organization_number_pk")),
                PartyId = reader.GetString(reader.GetOrdinal("party_id")),
                Created = reader.GetDateTime(reader.GetOrdinal("created"))
            };
        }
        return partyData;
    }

    public async Task InitializeParty(string organizationId, string partyId)
    {
        await using var connection = await _connectionProvider.GetConnectionAsync();
        await using (var command = await _connectionProvider.CreateCommand(
            "INSERT INTO broker.party (organization_number_pk, party_id, created) " +
            "VALUES (@organizationId, @partyId, NOW())"))
        {
            command.Parameters.AddWithValue("@organizationId", organizationId);
            command.Parameters.AddWithValue("@partyId", partyId);
            var commandText = command.CommandText;
            command.ExecuteNonQuery();
        }
    }
}

