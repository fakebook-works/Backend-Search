using BackEndSearchFakebook.Infrastructure.Security;
using Xunit;

namespace BackEndSearchFakebook.Tests;

public sealed class FixedTimeSecretComparerTests
{
    [Fact]
    public void IsStrongEnough_MeasuresUtf8Bytes()
    {
        Assert.False(FixedTimeSecretComparer.IsStrongEnough(new string('a', 31)));
        Assert.True(FixedTimeSecretComparer.IsStrongEnough(new string('a', 32)));
        Assert.True(FixedTimeSecretComparer.IsStrongEnough(new string('\u00e9', 16)));
    }

    [Fact]
    public void Matches_AcceptsOnlyTheExactStrongConfiguredSecret()
    {
        const string configured = "test-secret-that-is-at-least-32-bytes";

        Assert.True(FixedTimeSecretComparer.Matches(configured, configured));
        Assert.False(FixedTimeSecretComparer.Matches(
            "test-secret-that-is-at-least-32-byteX",
            configured));
        Assert.False(FixedTimeSecretComparer.Matches(string.Empty, configured));
        Assert.False(FixedTimeSecretComparer.Matches("short", "short"));
        Assert.False(FixedTimeSecretComparer.Matches(configured, null));
    }
}
