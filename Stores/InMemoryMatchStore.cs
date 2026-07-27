using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ResonanceServerOrchestrator.Configuration;
using ResonanceServerOrchestrator.Contracts;
using ResonanceServerOrchestrator.Services;

namespace ResonanceServerOrchestrator.Stores;

internal sealed class InMemoryMatchStore(IOptions<OrchestratorOptions> options, TimeProvider timeProvider)
    : IMatchStore
{
    private const int SecretLengthInBytes = 32;

    private readonly ConcurrentDictionary<Guid, MatchState> _matchesById = new();
    private readonly ConcurrentDictionary<LobbyKey, Guid> _matchIdByLobby = new();
    private readonly ConcurrentDictionary<PlayerIdentity, Guid> _matchIdByPlayer = new();
    private readonly ConcurrentDictionary<Guid, MatchTombstone> _tombstones = new();

    private readonly Dictionary<Guid, MatchDeadlineTimers> _deadlineTimersByMatchId = [];
    private readonly List<MemberCountSignal> _memberCountSignals = [];
    private readonly Lock _mutationLock = new();

    private long _lastIssuedMemberGeneration;

    private OrchestratorOptions Options => options.Value;

    public JoinOutcome TryJoin(
        LobbyKey lobby,
        PlayerIdentity identity,
        string username,
        IReadOnlyList<PlayerIdentity> expectedRoster)
    {
        lock (_mutationLock)
        {
            var multiLobbyRejection = EvictFromAnyMatchInAnotherLobby(lobby, identity, expectedRoster.Count);
            if (multiLobbyRejection is not null)
                return multiLobbyRejection;

            return TryFindMatchInLobby(lobby) is { } existingMatch
                ? JoinExistingMatch(existingMatch, identity, username, expectedRoster)
                : CreateMatch(lobby, identity, username, expectedRoster);
        }
    }

    public bool TrySetInstance(Guid matchId, IGameInstance instance)
    {
        lock (_mutationLock)
        {
            if (!_matchesById.TryGetValue(matchId, out var match))
                return false;

            _matchesById[matchId] = match with { Instance = instance };
            instance.Exited += (_, _) => OnInstanceExited(matchId);

            if (instance.HasExited)
                OnInstanceExited(matchId);

            return true;
        }
    }

    public MarkReadyOutcome MarkReady(Guid matchId, string presentedMatchKey)
    {
        lock (_mutationLock)
        {
            if (!_matchesById.TryGetValue(matchId, out var match))
                return DescribeAbsentMatch(matchId, presentedMatchKey);

            if (!MatchKeyIsCorrect(match.MatchKey, presentedMatchKey))
                return MarkReadyOutcome.MatchKeyRejected;

            switch (match.Status)
            {
                case MatchStatus.Pending:
                    return MarkReadyOutcome.RosterNotYetComplete;
                case MatchStatus.Started:
                    return MarkReadyOutcome.MatchWasAlreadyStarted;
            }

            DeadlineTimersFor(matchId)?.DisposeServerReadyTimer();

            var started = match with { Status = MatchStatus.Started, ReadyAt = timeProvider.GetUtcNow() };
            _matchesById[matchId] = started;

            foreach (var member in started.Members.Values)
                member.Completion.TrySetResult(new JoinSucceeded(
                    matchId, Options.GameServerHost, Options.GameServerPort, member.ServerAuthToken));

            return MarkReadyOutcome.MatchStarted;
        }
    }

    public MatchSnapshotLookup LookUpSnapshotForGameServer(Guid matchId, string presentedMatchKey)
    {
        if (!_matchesById.TryGetValue(matchId, out var match))
        {
            if (!_tombstones.TryGetValue(matchId, out var tombstone))
                return new MatchSnapshotLookup(MatchSnapshotLookupOutcome.MatchNotFound, null);

            return new MatchSnapshotLookup(
                MatchKeyHashIsCorrect(tombstone.MatchKeyHash, presentedMatchKey)
                    ? MatchSnapshotLookupOutcome.MatchAlreadyDestroyed
                    : MatchSnapshotLookupOutcome.MatchKeyRejected,
                null);
        }

        return MatchKeyIsCorrect(match.MatchKey, presentedMatchKey)
            ? new MatchSnapshotLookup(MatchSnapshotLookupOutcome.Granted, CreateSnapshot(match))
            : new MatchSnapshotLookup(MatchSnapshotLookupOutcome.MatchKeyRejected, null);
    }

    public bool TryTearDownForFailedAuth(LobbyKey lobby, PlayerIdentity claimedIdentity)
    {
        lock (_mutationLock)
        {
            if (TryFindMatchInLobby(lobby) is not { } match ||
                match.Status is MatchStatus.Started ||
                !match.CanonicalRoster.Contains(claimedIdentity))
                return false;

            DestroyWithFailure(match, JoinFailureReason.PeerAuthenticationFailed);
            return true;
        }
    }

    public bool TryLeave(PlayerIdentity identity)
    {
        lock (_mutationLock)
        {
            if (TryFindMatchOf(identity) is not { } match)
                return false;

            if (match.Status is MatchStatus.Started)
                RemoveMembershipFromStartedMatch(match, identity);
            else
                DestroyWithFailure(match, JoinFailureReason.PeerLeft);

            return true;
        }
    }

    public void DeregisterAbortedMember(Guid matchId, PlayerIdentity identity, long memberGeneration)
    {
        lock (_mutationLock)
        {
            if (!_matchesById.TryGetValue(matchId, out var match) ||
                !match.Members.TryGetValue(identity, out var member) ||
                member.MemberGeneration != memberGeneration)
                return;

            member.Completion.TrySetResult(
                new JoinFailed(JoinFailureReason.PeerLeft, match.JoinedCount, match.ExpectedCount));

            if (match.Status is not MatchStatus.Pending)
                return;

            var remaining = match.WithoutMember(identity);
            _matchIdByPlayer.TryRemove(new KeyValuePair<PlayerIdentity, Guid>(identity, matchId));

            if (remaining.Members.IsEmpty)
            {
                DestroyWithFailure(match, JoinFailureReason.PeerLeft);
                return;
            }

            _matchesById[matchId] = remaining;
        }
    }

    public void OnInstanceExited(Guid matchId)
    {
        lock (_mutationLock)
        {
            if (!_matchesById.TryGetValue(matchId, out var match))
                return;

            if (match.Status is MatchStatus.Started)
                Destroy(matchId, OutcomeForAMatchExpectedToHaveNoWaiters);
            else
                DestroyWithFailure(match, JoinFailureReason.ServerLaunchFailed);
        }
    }

    public void ReapExpired()
    {
        lock (_mutationLock)
        {
            var now = timeProvider.GetUtcNow();
            var startedMatchLifetime = TimeSpan.FromMinutes(Options.MatchTimeoutMinutes);

            foreach (var match in _matchesById.Values.ToArray())
                if (match.Status is MatchStatus.Started &&
                    match.ReadyAt is { } readyAt &&
                    now - readyAt >= startedMatchLifetime)
                    Destroy(match.MatchId, OutcomeForAMatchExpectedToHaveNoWaiters);

            var tombstoneRetention = TimeSpan.FromMinutes(Options.TombstoneRetentionMinutes);

            foreach (var (matchId, tombstone) in _tombstones.ToArray())
                if (now - tombstone.DestroyedAt >= tombstoneRetention)
                    _tombstones.TryRemove(new KeyValuePair<Guid, MatchTombstone>(matchId, tombstone));
        }
    }

    internal Task<Guid> WhenMemberCountReaches(LobbyKey lobby, int memberCount)
    {
        lock (_mutationLock)
        {
            if (TryFindMatchInLobby(lobby) is { } match && match.JoinedCount >= memberCount)
                return Task.FromResult(match.MatchId);

            var signal = new MemberCountSignal(
                lobby,
                memberCount,
                new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously));
            _memberCountSignals.Add(signal);
            return signal.Completion.Task;
        }
    }

    internal MatchState? FindMatch(Guid matchId) =>
        _matchesById.GetValueOrDefault(matchId);

    internal MatchState? FindMatchInLobby(LobbyKey lobby) =>
        TryFindMatchInLobby(lobby);

    internal int LiveMatchCount => _matchesById.Count;

    private Rejected? EvictFromAnyMatchInAnotherLobby(
        LobbyKey lobby,
        PlayerIdentity identity,
        int expectedRosterSize)
    {
        if (TryFindMatchOf(identity) is not { } otherMatch || otherMatch.Lobby.Equals(lobby))
            return null;

        if (otherMatch.Status is MatchStatus.Started)
            RemoveMembershipFromStartedMatch(otherMatch, identity);
        else
            DestroyWithFailure(otherMatch, JoinFailureReason.PeerLeft);

        return new Rejected(JoinFailureReason.PlayerInMultipleLobbies, 0, expectedRosterSize);
    }

    private JoinOutcome JoinExistingMatch(
        MatchState match,
        PlayerIdentity identity,
        string username,
        IReadOnlyList<PlayerIdentity> expectedRoster)
    {
        if (!match.CanonicalRosterMatches(expectedRoster))
        {
            var rejection = new Rejected(
                JoinFailureReason.RosterMismatch, match.JoinedCount, match.ExpectedCount);
            DestroyWithFailure(match, JoinFailureReason.RosterMismatch);
            return rejection;
        }

        if (match.Status is MatchStatus.Started)
            return new Rejected(
                JoinFailureReason.MatchAlreadyStarted, match.JoinedCount, match.ExpectedCount);

        return match.Members.ContainsKey(identity)
            ? ReplaceMember(match, identity, username)
            : AddMember(match, identity, username);
    }

    private JoinOutcome ReplaceMember(MatchState match, PlayerIdentity identity, string username)
    {
        var supersededMember = match.Members[identity];
        var replacement = RegisterMember(identity, username);
        var updated = match.WithMember(replacement);

        _matchesById[match.MatchId] = updated;
        _matchIdByPlayer[identity] = match.MatchId;

        supersededMember.Completion.TrySetResult(new JoinFailed(
            JoinFailureReason.SupersededByReconnect, updated.JoinedCount, updated.ExpectedCount));

        return new MemberAdded(match.MatchId, replacement.MemberGeneration, replacement.Completion.Task);
    }

    private JoinOutcome AddMember(MatchState match, PlayerIdentity identity, string username)
    {
        var member = RegisterMember(identity, username);
        var updated = match.WithMember(member);
        _matchIdByPlayer[identity] = match.MatchId;
        return PublishJoinedMatch(updated, member);
    }

    private JoinOutcome CreateMatch(
        LobbyKey lobby,
        PlayerIdentity identity,
        string username,
        IReadOnlyList<PlayerIdentity> expectedRoster)
    {
        if (_matchesById.Count >= Options.MaxMatches)
            return new Rejected(JoinFailureReason.CapacityReached, 0, 0);

        var matchId = Guid.NewGuid();
        var firstMember = RegisterMember(identity, username);
        var match = new MatchState
        {
            MatchId = matchId,
            Lobby = lobby,
            Status = MatchStatus.Pending,
            MatchKey = MintSecret(),
            CanonicalRoster = expectedRoster.ToImmutableArray(),
            CreatedAt = timeProvider.GetUtcNow(),
            Members = ImmutableDictionary<PlayerIdentity, MatchMember>.Empty.Add(identity, firstMember)
        };

        _deadlineTimersByMatchId[matchId] = new MatchDeadlineTimers();
        _matchIdByLobby[lobby] = matchId;
        _matchIdByPlayer[identity] = matchId;

        return PublishJoinedMatch(match, firstMember);
    }

    private JoinOutcome PublishJoinedMatch(MatchState match, MatchMember joiningMember)
    {
        var timers = _deadlineTimersByMatchId[match.MatchId];

        if (match.Status is MatchStatus.Pending && match.RosterIsComplete)
        {
            timers.DisposeRosterAssemblyTimer();
            timers.ArmServerReadyTimer(
                timeProvider,
                TimeSpan.FromSeconds(Options.ServerReadyTimeoutSeconds),
                () => ExpireServerReadyDeadline(match.MatchId));

            var launching = match with { Status = MatchStatus.Launching };
            _matchesById[match.MatchId] = launching;
            SignalMemberCountReached(launching);

            return new RosterComplete(
                launching.MatchId,
                joiningMember.MemberGeneration,
                CreateSnapshot(launching),
                joiningMember.Completion.Task);
        }

        _matchesById[match.MatchId] = match;

        if (match.Status is MatchStatus.Pending)
            timers.ArmRosterAssemblyTimerOnce(
                timeProvider,
                TimeSpan.FromSeconds(Options.RosterAssemblyTimeoutSeconds),
                () => ExpireRosterAssemblyDeadline(match.MatchId));

        SignalMemberCountReached(match);
        return new MemberAdded(match.MatchId, joiningMember.MemberGeneration, joiningMember.Completion.Task);
    }

    private void RemoveMembershipFromStartedMatch(MatchState match, PlayerIdentity identity)
    {
        var remaining = match.WithoutMember(identity);
        _matchIdByPlayer.TryRemove(new KeyValuePair<PlayerIdentity, Guid>(identity, match.MatchId));

        if (remaining.Members.IsEmpty)
        {
            Destroy(match.MatchId, OutcomeForAMatchExpectedToHaveNoWaiters);
            return;
        }

        _matchesById[match.MatchId] = remaining;
    }

    private void ExpireRosterAssemblyDeadline(Guid matchId)
    {
        lock (_mutationLock)
        {
            if (_matchesById.TryGetValue(matchId, out var match) && match.Status is MatchStatus.Pending)
                DestroyWithFailure(match, JoinFailureReason.RosterAssemblyTimedOut);
        }
    }

    private void ExpireServerReadyDeadline(Guid matchId)
    {
        lock (_mutationLock)
        {
            if (_matchesById.TryGetValue(matchId, out var match) && match.Status is MatchStatus.Launching)
                DestroyWithFailure(match, JoinFailureReason.ServerReadyTimedOut);
        }
    }

    private void DestroyWithFailure(MatchState match, JoinFailureReason reason)
    {
        var failure = new JoinFailed(reason, match.JoinedCount, match.ExpectedCount);
        Destroy(match.MatchId, _ => failure);
    }

    private void Destroy(Guid matchId, Func<PlayerIdentity, JoinResult> outcomeFor)
    {
        if (!_matchesById.TryGetValue(matchId, out var match))
            return;

        if (_deadlineTimersByMatchId.Remove(matchId, out var timers))
            timers.DisposeAll();

        foreach (var member in match.Members.Values)
            member.Completion.TrySetResult(outcomeFor(member.Identity));

        match.Instance?.Stop();

        _matchesById.TryRemove(matchId, out _);
        _matchIdByLobby.TryRemove(new KeyValuePair<LobbyKey, Guid>(match.Lobby, matchId));

        foreach (var identity in match.Members.Keys)
            _matchIdByPlayer.TryRemove(new KeyValuePair<PlayerIdentity, Guid>(identity, matchId));

        _tombstones[matchId] = new MatchTombstone(HashMatchKey(match.MatchKey), timeProvider.GetUtcNow());
    }

    private MatchState? TryFindMatchInLobby(LobbyKey lobby) =>
        _matchIdByLobby.TryGetValue(lobby, out var matchId) ? _matchesById.GetValueOrDefault(matchId) : null;

    private MatchState? TryFindMatchOf(PlayerIdentity identity) =>
        _matchIdByPlayer.TryGetValue(identity, out var matchId) ? _matchesById.GetValueOrDefault(matchId) : null;

    private MatchDeadlineTimers? DeadlineTimersFor(Guid matchId) =>
        _deadlineTimersByMatchId.GetValueOrDefault(matchId);

    private MatchMember RegisterMember(PlayerIdentity identity, string username) =>
        MatchMember.Register(identity, username, MintSecret(), ++_lastIssuedMemberGeneration);

    private MatchSnapshot CreateSnapshot(MatchState match) =>
        new(match.MatchId, match.MatchKey, Options.GameServerPort, match.MembersInCanonicalRosterOrder());

    private MarkReadyOutcome DescribeAbsentMatch(Guid matchId, string presentedMatchKey)
    {
        if (!_tombstones.TryGetValue(matchId, out var tombstone))
            return MarkReadyOutcome.MatchNotFound;

        return MatchKeyHashIsCorrect(tombstone.MatchKeyHash, presentedMatchKey)
            ? MarkReadyOutcome.MatchAlreadyDestroyed
            : MarkReadyOutcome.MatchKeyRejected;
    }

    private void SignalMemberCountReached(MatchState match)
    {
        for (var index = _memberCountSignals.Count - 1; index >= 0; index--)
        {
            var signal = _memberCountSignals[index];
            if (!signal.Lobby.Equals(match.Lobby) || match.JoinedCount < signal.MemberCount)
                continue;

            _memberCountSignals.RemoveAt(index);
            signal.Completion.TrySetResult(match.MatchId);
        }
    }

    private static JoinResult OutcomeForAMatchExpectedToHaveNoWaiters(PlayerIdentity _) =>
        new JoinFailed(JoinFailureReason.PeerLeft, 0, 0);

    private static string MintSecret() =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretLengthInBytes));

    private static byte[] HashMatchKey(string matchKey) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(matchKey));

    private static bool MatchKeyIsCorrect(string storedMatchKey, string presentedMatchKey) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(storedMatchKey), Encoding.UTF8.GetBytes(presentedMatchKey));

    private static bool MatchKeyHashIsCorrect(byte[] storedMatchKeyHash, string presentedMatchKey) =>
        CryptographicOperations.FixedTimeEquals(storedMatchKeyHash, HashMatchKey(presentedMatchKey));

    private sealed record MemberCountSignal(
        LobbyKey Lobby,
        int MemberCount,
        TaskCompletionSource<Guid> Completion);
}
