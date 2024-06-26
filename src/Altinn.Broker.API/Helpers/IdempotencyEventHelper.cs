using Altinn.Broker.Application;
using Altinn.Broker.Core.Repositories;

using OneOf;

namespace Altinn.Broker.Helpers;

public class IdempotencyEventHelper
{
    public static async Task<OneOf<Task, Error>> ProcessEvent(string uniqueString, Func<Task<OneOf<Task, Error>>> process, IIdempotencyEventRepository idempotencyEventRepository, CancellationToken cancellationToken)
    {
        try
        {

            // Create a new entry for that webhook id
            await idempotencyEventRepository.AddIdempotencyEventAsync(uniqueString, cancellationToken);
            try
            {
                // Call you method
                return await process();
            }
            catch (Exception)
            {
                // Delete the entry on error to make sure the next one isn't ignored
                await idempotencyEventRepository.DeleteIdempotencyEventAsync(uniqueString, cancellationToken);
                throw;
            }
        }
        catch (Npgsql.PostgresException e)
        {
            // PostgreSQL code for unique violation
            if (e.SqlState == "23505")
            {
                // We are already processing or processed this webhook, return silently
                return Task.CompletedTask;
            }
            else throw;

        }
    }
}

