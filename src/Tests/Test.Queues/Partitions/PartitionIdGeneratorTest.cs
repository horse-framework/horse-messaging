using System.Threading;
using System.Linq;
using System.Collections.Generic;
using Horse.Messaging.Server.Queues.Partitions;
using Xunit;

namespace Test.Queues.Partitions;

/// <summary>
/// Pure unit tests for PartitionIdGenerator — no server needed.
/// </summary>
public class PartitionIdGeneratorTest
{
    [Fact]
    public void Generate_Returns6CharString()
    {
        string id = PartitionIdGenerator.Generate();
        Assert.Equal(6, id.Length);
    }

    [Fact]
    public void Generate_ContainsOnlyBase62Chars()
    {
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        for (int i = 0; i < 10000; i++)
        {
            string id = PartitionIdGenerator.Generate();
            Assert.True(id.All(c => alphabet.Contains(c)),
                $"Generated ID '{id}' contains non-base62 character");
        }
    }

    [Fact]
    public void Generate_IsNotEmpty()
    {
        string id = PartitionIdGenerator.Generate();
        Assert.False(string.IsNullOrWhiteSpace(id));
    }

    [Fact]
    public void Generate_ProducesUniqueValues()
    {
        // Generate 100k IDs and expect very high uniqueness
        const int count = 100_000;
        var ids = new HashSet<string>(count);

        for (int i = 0; i < count; i++)
            ids.Add(PartitionIdGenerator.Generate());

        // Expecting near 100% uniqueness; collision chance is ~ count^2 / 2*62^6 ≈ 0.09%
        double uniqueRatio = (double)ids.Count / count;
        Assert.True(uniqueRatio > 0.99,
            $"Uniqueness ratio {uniqueRatio:P2} is too low (expected > 99%)");
    }

    [Fact]
    public void Generate_IsUrlSafe()
    {
        // Should not contain any character requiring URL encoding
        for (int i = 0; i < 1000; i++)
        {
            string id = PartitionIdGenerator.Generate();
            Assert.DoesNotContain("/", id);
            Assert.DoesNotContain("+", id);
            Assert.DoesNotContain("=", id);
            Assert.DoesNotContain(" ", id);
        }
    }
}

