using System.Security.Cryptography;
using System.Text;

namespace GroupsOnTaskbar.Core.Launch;

public static class IconCacheKey
{
    public static string Create(string path, DateTimeOffset lastWriteUtc)
    {
        var input = string.Concat(
            Path.GetFullPath(path).ToUpperInvariant(),
            "|",
            lastWriteUtc.UtcTicks);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"{Convert.ToHexStringLower(digest)}.png";
    }
}
