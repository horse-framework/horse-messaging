using System;

namespace Horse.Messaging.Server.Queues.Partitions;

/// <summary>
/// Generates short, URL-safe, collision-resistant partition identifiers.
/// Uses base-62 encoding of a random 32-bit value to produce a 6-character string.
/// 62^6 ≈ 56 billion combinations; expected first collision beyond 9 million IDs.
/// </summary>
public static class PartitionIdGenerator
{
    private static readonly char[] Alphabet =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();

    /// <summary>Generates a 6-character base-62 identifier.</summary>
    public static string Generate()
    {
        Span<char> result = stackalloc char[6];
        uint value = (uint)Random.Shared.Next();
        for (int i = 5; i >= 0; i--)
        {
            result[i] = Alphabet[value % 62];
            value /= 62;
        }
        return new string(result);
    }
}
