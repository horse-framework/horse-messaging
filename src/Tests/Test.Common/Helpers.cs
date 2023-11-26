using System;

namespace Test.Common;

public static class Helpers
{
    private static readonly Random _random = new();

    public static int GetRandom()
    {
        return _random.Next(100, 5000000);
    }
}