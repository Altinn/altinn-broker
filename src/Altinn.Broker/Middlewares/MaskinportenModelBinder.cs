using Altinn.Broker.Core.Domain;
using Altinn.Broker.Models.Maskinporten;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Altinn.Broker.Middlewares;

/**
 * This binder is used to get claims from Maskinporten token and make it available as a controller parameter 
 * */
public class MaskinportenModelBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var consumerOrgNo = Helpers.MaskinportenHelper.GetConsumerFromToken(bindingContext.HttpContext);
        var scope = Helpers.MaskinportenHelper.GetScopeFromToken(bindingContext.HttpContext);
        var clientId = Helpers.MaskinportenHelper.GetClientIdFromToken(bindingContext.HttpContext);
        if (string.IsNullOrWhiteSpace(consumerOrgNo) || string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(clientId))
        {
            throw new BadHttpRequestException("Malformed bearer token. It should contain the claims 'consumer', 'client_id' and 'scope'.");
        }

        bindingContext.Result = ModelBindingResult.Success(new CallerIdentity(scope, consumerOrgNo, clientId));
        await Task.CompletedTask;
    }
}
