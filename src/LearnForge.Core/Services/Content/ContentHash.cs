using System.Security.Cryptography;
using System.Text;

namespace LearnForge.Core;

public static class ContentHash
{
    // Uses the canonical serializer, so the hash changes only when content changes.
    public static string Of<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json.Write(value))));
}
