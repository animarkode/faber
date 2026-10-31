using System.Security.Cryptography;
using System.Text;

namespace Faber.AppHost;

public static class PostgresVolumeName
{
    public static string FromCheckoutPath(string checkoutPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkoutPath);

        var canonicalPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(checkoutPath));
        var pathHash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPath));
        // Docker volume names must be lowercase, and the name is the identity of the user's existing volume.
        // CA1308 asks for upper-case normalization, which would rename the volume and orphan its data.
#pragma warning disable CA1308
        var suffix = Convert.ToHexString(pathHash.AsSpan(0, 8)).ToLowerInvariant();
#pragma warning restore CA1308

        return $"faber-postgres-data-{suffix}";
    }
}
