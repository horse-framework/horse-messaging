using System;

namespace Horse.Messaging.Server.Helpers;

internal class Filter
{
    public static bool CheckMatch(string value, string filter)
    {
        ReadOnlySpan<char> pattern = filter.AsSpan();
        if (pattern.Length == 1 && pattern[0] == '*')
            return true;
        bool jstart = pattern.Length > 0 && pattern[0] == '*';
        bool jend = pattern.Length > 0 && pattern[^1] == '*';

        if (jstart && jend)
            return value.AsSpan().Contains(pattern[1..^1], StringComparison.OrdinalIgnoreCase);

        if (jstart)
            return value.AsSpan().EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);

        if (jend)
            return value.AsSpan().StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);

        return value.AsSpan().Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks queue, router or client name if it's eligible
    /// </summary>
    public static bool CheckNameEligibility(string name)
    {
        if (name.Contains('@') || name.Contains(' ') || name.Contains(';') || name.Contains('*'))
            return false;

        return true;
    }
}
