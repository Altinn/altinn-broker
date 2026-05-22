using Altinn.Broker.Core.Services;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Altinn.Broker.Integrations.Maskinporten;

public class MaskinportenJwkGenerator : IMaskinportenJwkGenerator
{
    public MaskinportenGeneratedJwk Generate(string keyIdPrefix)
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(true);
        var kid = $"{keyIdPrefix}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

        var publicJwk = new MaskinportenJwkKey
        {
            Kty = "RSA",
            Use = "sig",
            Alg = "RS256",
            Kid = kid,
            N = Base64UrlEncoder.Encode(parameters.Modulus),
            E = Base64UrlEncoder.Encode(parameters.Exponent)
        };

        var privateJwk = new Dictionary<string, string>
        {
            ["kty"] = publicJwk.Kty,
            ["use"] = publicJwk.Use,
            ["alg"] = publicJwk.Alg,
            ["kid"] = publicJwk.Kid,
            ["n"] = publicJwk.N,
            ["e"] = publicJwk.E,
            ["d"] = Base64UrlEncoder.Encode(parameters.D!),
            ["p"] = Base64UrlEncoder.Encode(parameters.P!),
            ["q"] = Base64UrlEncoder.Encode(parameters.Q!),
            ["dp"] = Base64UrlEncoder.Encode(parameters.DP!),
            ["dq"] = Base64UrlEncoder.Encode(parameters.DQ!),
            ["qi"] = Base64UrlEncoder.Encode(parameters.InverseQ!)
        };

        var privateJwkJson = JsonSerializer.Serialize(privateJwk);
        return new MaskinportenGeneratedJwk
        {
            Kid = kid,
            PrivateJwkJson = privateJwkJson,
            PrivateJwkBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(privateJwkJson)),
            PublicJwk = publicJwk
        };
    }

    public MaskinportenJwkKey GetPublicKey(string encodedPrivateJwk)
    {
        var privateJwk = DecodePrivateJwk(encodedPrivateJwk);
        return new MaskinportenJwkKey
        {
            Kty = privateJwk["kty"],
            Use = privateJwk["use"],
            Alg = privateJwk["alg"],
            Kid = privateJwk["kid"],
            N = privateJwk["n"],
            E = privateJwk["e"]
        };
    }

    private static Dictionary<string, string> DecodePrivateJwk(string encodedPrivateJwk)
    {
        if (string.IsNullOrWhiteSpace(encodedPrivateJwk))
        {
            throw new InvalidOperationException("Encoded private JWK is missing.");
        }

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPrivateJwk));
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new InvalidOperationException("Unable to deserialize private JWK.");
    }
}
