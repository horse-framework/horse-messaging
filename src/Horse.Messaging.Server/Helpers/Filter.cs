using System;

namespace Horse.Messaging.Server.Helpers;

internal class Filter
{
    public static bool CheckMatch(string value, string filter)
    {
        bool jstart = filter.StartsWith('*');
        bool jend = filter.EndsWith('*');

        if (jstart && jend)
            return value.Contains(filter.Substring(1, filter.Length - 2), StringComparison.OrdinalIgnoreCase);

        if (jstart)
            return value.EndsWith(filter.Substring(1), StringComparison.OrdinalIgnoreCase);

        if (jend)
            return value.StartsWith(filter.Substring(0, filter.Length - 1), StringComparison.OrdinalIgnoreCase);

        return value.Equals(filter, StringComparison.OrdinalIgnoreCase);
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