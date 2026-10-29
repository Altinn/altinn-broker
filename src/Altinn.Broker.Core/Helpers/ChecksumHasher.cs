using System.Security.Cryptography;

using Altinn.Broker.Core.Domain.Enums;

namespace Altinn.Broker.Core.Helpers;

public static class ChecksumHasher
{
    public static HashAlgorithm Create(ChecksumAlgorithm algorithm) => algorithm switch
    {
        ChecksumAlgorithm.SHA256 => SHA256.Create(),
        _ => MD5.Create()
    };

    public static string ToLowerHex(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();

    public static async Task<string> ComputeAsync(Stream stream, ChecksumAlgorithm algorithm, CancellationToken cancellationToken)
    {
        using var hashAlgorithm = Create(algorithm);
        var hash = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken);
        return ToLowerHex(hash);
    }

    public static ChecksumAlgorithm Parse(string? value)
    {
        if (Enum.TryParse<ChecksumAlgorithm>(value, ignoreCase: true, out var algorithm)
            && Enum.IsDefined(algorithm))
        {
            return algorithm;
        }

        return ChecksumAlgorithm.MD5;
    }
}
