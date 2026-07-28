#nullable enable
using System;

namespace Resonance.Contracts
{
    /// <summary>
    /// Identifies one player across the orchestrator. Used as a dictionary key and as the basis
    /// of roster matching, so its equality is load-bearing rather than incidental.
    /// </summary>
    /// <remarks>
    /// Hand-written rather than a record struct: record structs are C# 10, above the C# 9
    /// ceiling Unity compiles this package with.
    /// </remarks>
    public readonly struct PlayerIdentity : IEquatable<PlayerIdentity>
    {
        public PlayerIdentity(Platform platform, string platformUserId)
        {
            Platform = platform;
            PlatformUserId = platformUserId;
        }

        public Platform Platform { get; }

        public string PlatformUserId { get; }

        public bool Equals(PlayerIdentity other) =>
            Platform == other.Platform &&
            string.Equals(PlatformUserId, other.PlatformUserId, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is PlayerIdentity other && Equals(other);

        public override int GetHashCode() =>
            ((int)Platform * 397) ^ (PlatformUserId is null ? 0 : PlatformUserId.GetHashCode());

        public static bool operator ==(PlayerIdentity left, PlayerIdentity right) =>
            left.Equals(right);

        public static bool operator !=(PlayerIdentity left, PlayerIdentity right) =>
            !left.Equals(right);

        public override string ToString() => Platform + ":" + PlatformUserId;
    }
}
