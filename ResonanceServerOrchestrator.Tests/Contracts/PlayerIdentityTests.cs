using ResonanceServerOrchestrator.Contracts;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Contracts;

/// <summary>
/// PlayerIdentity is a dictionary key in InMemoryMatchStore and the basis of roster matching,
/// so its equality is load-bearing rather than incidental.
/// </summary>
public sealed class PlayerIdentityTests
{
    [Fact]
    public void Identities_WithTheSamePlatformAndUserId_AreEqual()
    {
        var left = new PlayerIdentity(Platform.Steam, "765");
        var right = new PlayerIdentity(Platform.Steam, "765");

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Identities_DifferingOnlyByPlatform_AreNotEqual()
    {
        var steam = new PlayerIdentity(Platform.Steam, "765");
        var dummy = new PlayerIdentity(Platform.Dummy, "765");

        Assert.NotEqual(steam, dummy);
        Assert.False(steam == dummy);
    }

    [Fact]
    public void Identities_DifferingOnlyByUserId_AreNotEqual()
    {
        Assert.NotEqual(
            new PlayerIdentity(Platform.Steam, "765"),
            new PlayerIdentity(Platform.Steam, "766"));
    }

    [Fact]
    public void UserIdComparison_IsOrdinal()
    {
        Assert.NotEqual(
            new PlayerIdentity(Platform.Steam, "abc"),
            new PlayerIdentity(Platform.Steam, "ABC"));
    }

    [Fact]
    public void Identities_WorkAsDictionaryKeys()
    {
        var map = new Dictionary<PlayerIdentity, string>
        {
            [new PlayerIdentity(Platform.Steam, "765")] = "steam-player",
            [new PlayerIdentity(Platform.Dummy, "765")] = "dummy-player",
        };

        Assert.Equal("steam-player", map[new PlayerIdentity(Platform.Steam, "765")]);
        Assert.Equal("dummy-player", map[new PlayerIdentity(Platform.Dummy, "765")]);
    }

    [Fact]
    public void Equals_AgainstAnUnrelatedType_IsFalse()
    {
        Assert.False(new PlayerIdentity(Platform.Steam, "765").Equals("765"));
    }
}
