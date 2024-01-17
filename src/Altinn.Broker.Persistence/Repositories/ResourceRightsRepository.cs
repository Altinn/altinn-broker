using Altinn.Broker.Core.Domain.Enums;
using Altinn.Broker.Core.Repositories;

namespace Altinn.Broker.Persistence.Repositories;
public class ResourceRightsRepository : IResourceRightsRepository
{
    private readonly DatabaseConnectionProvider _connectionProvider;

    public ResourceRightsRepository(DatabaseConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<bool> CheckOrganizationsHasAccess(List<string> organizationNumbers)
    {
        using var command = await _connectionProvider.CreateCommand(
            "SELECT EXISTS(SELECT 1 FROM broker.user " +
            "WHERE organization_number = ANY(@organizationNumbers))");
        command.Parameters.AddWithValue("@organizationNumbers", organizationNumbers);

        var result = (bool) (command.ExecuteScalar() ?? false);
        return result;
    }

    public async Task<bool> CheckUserAccess(string resourceId, string userId, ResourceAccessLevel right)
    {
        using var command = await _connectionProvider.CreateCommand(
            "SELECT EXISTS(SELECT 1 FROM broker.user_right " +
            "JOIN broker.user_right_description ON broker.user_right.user_right_description_id_fk = broker.user_right_description.user_right_description_id_pk " +
            "LEFT JOIN broker.resource r on broker.user_right.resource_id_fk = r.resource_id_pk " +
            "WHERE r.resource_id_pk = @resourceId AND user_id_fk = @userId AND user_right_description = @right)");
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@right", right.ToString());

        var result = (bool)(command.ExecuteScalar() ?? false);
        return result;
    }

    public async Task GiveUserAccess(string userId, string resourceId, string right, string behalfOfOrganization)
    {
        await CreateUserIfNotExist(userId, behalfOfOrganization);
        using var command = await _connectionProvider.CreateCommand(
            "INSERT INTO broker.user_right (user_id_fk, resource_id_fk, user_right_description_id_fk) " +
            "SELECT @userId, @resourceId, user_right_description_id_pk FROM broker.user_right_description " +
            "WHERE user_right_description = @right");
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@resourceId", resourceId);
        command.Parameters.AddWithValue("@right", right);

        command.ExecuteNonQuery();
    }

    private async Task CreateUserIfNotExist(string userId, string behalfOfOrganization)
    {
        using var checkUserCommand = await _connectionProvider.CreateCommand(
            "SELECT EXISTS(SELECT 1 FROM broker.user WHERE client_id_pk = @userId)");
        checkUserCommand.Parameters.AddWithValue("@userId", userId);

        var userExists = (bool)(checkUserCommand.ExecuteScalar() ?? false);
        if (!userExists)
        {
            using var insertUserCommand = await _connectionProvider.CreateCommand(
                "INSERT INTO broker.user (client_id_pk, organization_number) VALUES (@userId, @behalfOfOrganization)");
            insertUserCommand.Parameters.AddWithValue("@userId", userId);
            insertUserCommand.Parameters.AddWithValue("@behalfOfOrganization", behalfOfOrganization);

            insertUserCommand.ExecuteNonQuery();
        }
    }

}
