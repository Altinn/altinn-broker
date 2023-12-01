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
        var consumerOrgNo = Helpers.ClaimHelper.GetConsumerFromToken(bindingContext.HttpContext);
        var scope = Helpers.ClaimHelper.GetScopeFromToken(bindingContext.HttpContext);
        if (string.IsNullOrWhiteSpace(consumerOrgNo) || string.IsNullOrWhiteSpace(scope))
        {
            throw new BadHttpRequestException("Malformed bearer token. It should contain the claims 'consumer' and 'scope'.");
        }

        var supplierOrgNo = Helpers.ClaimHelper.GetSupplierFromToken(bindingContext.HttpContext);
        if (string.IsNullOrWhiteSpace(supplierOrgNo))
        {
            supplierOrgNo = consumerOrgNo; // In the case where service owner uses his own token
        }

        bindingContext.Result = ModelBindingResult.Success(new MaskinportenToken(scope, consumerOrgNo, supplierOrgNo));
        await Task.CompletedTask;
    }
}
