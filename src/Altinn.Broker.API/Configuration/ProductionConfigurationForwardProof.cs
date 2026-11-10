using System.Security.Cryptography;
using System.Text;

using Altinn.Broker.Core.Options;
using Altinn.Broker.Core.Services;

using Microsoft.Extensions.Options;

namespace Altinn.Broker.API.Configuration;

/// <summary>
/// TT02 treats a production end-user token as already publish-checked only when the
/// production Broker presents the shared forward key. The caller's organization is not proof.
/// </summary>
public sealed class ProductionConfigurationForwardProof : IProductionConfigurationForwardProof
{
    public const string HeaderName = "Broker-Production-Forward-Key";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _expectedKey;

    public ProductionConfigurationForwardProof(
        IHttpContextAccessor httpContextAccessor,
        IOptions<AltinnOptions> altinnOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _expectedKey = altinnOptions.Value.StagingConfigurationForwardKey.Trim();
    }

    public bool IsVerified
    {
        get
        {
            if (_expectedKey.Length == 0)
            {
                return false;
            }

            var providedValues = _httpContextAccessor.HttpContext?.Request.Headers[HeaderName];
            if (providedValues is null || providedValues.Value.Count != 1)
            {
                return false;
            }

            var provided = providedValues.Value.ToString();
            if (provided.Length == 0)
            {
                return false;
            }

            var expectedBytes = Encoding.UTF8.GetBytes(_expectedKey);
            var providedBytes = Encoding.UTF8.GetBytes(provided);
            return expectedBytes.Length == providedBytes.Length
                && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
        }
    }
}
