using System.Collections.Immutable;
using Resonance.Contracts;
using ResonanceServerOrchestrator.Services;

namespace ResonanceServerOrchestrator.Stores;

internal sealed record MatchState
{
    public required Guid MatchId { get; init; }
    public required LobbyKey Lobby { get; init; }
    public required MatchStatus Status { get; init; }
    public required string MatchKey { get; init; }
    public required IReadOnlyList<PlayerIdentity> CanonicalRoster { get; init; }
    public required string NextSceneName { get; init; }
    public required string GameMode { get; init; }
    public required string? IntendedServerVersion { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required ImmutableDictionary<PlayerIdentity, MatchMember> Members { get; init; }
    public DateTimeOffset? ReadyAt { get; init; }
    public IGameInstance? Instance { get; init; }

    public int JoinedCount => Members.Count;

    public int ExpectedCount => CanonicalRoster.Count;

    public bool RosterIsComplete => Members.Count >= CanonicalRoster.Count;

    public bool CanonicalRosterMatches(IReadOnlyCollection<PlayerIdentity> claimedRoster) =>
        claimedRoster.Count == CanonicalRoster.Count &&
        CanonicalRoster.ToHashSet().SetEquals(claimedRoster);

    public MatchState WithMember(MatchMember member) =>
        this with { Members = Members.SetItem(member.Identity, member) };

    public MatchState WithoutMember(PlayerIdentity identity) =>
        this with { Members = Members.Remove(identity) };

    public IReadOnlyList<MatchMemberDto> MembersInCanonicalRosterOrder() =>
        CanonicalRoster
            .Where(Members.ContainsKey)
            .Select(identity => Members[identity].ToDto())
            .ToImmutableArray();
}