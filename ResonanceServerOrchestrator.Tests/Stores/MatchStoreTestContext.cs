using Microsoft.Extensions.Time.Testing;
using ResonanceServerOrchestrator.Configuration;
using Resonance.Contracts;
using ResonanceServerOrchestrator.Stores;
using Xunit;

namespace ResonanceServerOrchestrator.Tests.Stores;

internal sealed class MatchStoreTestContext
{
    private static readonly TimeSpan WaiterCompletionBudget = TimeSpan.FromSeconds(10);

    public static readonly LobbyKey FirstLobby = new(Platform.Steam, "lobby-one");
    public static readonly LobbyKey SecondLobby = new(Platform.Steam, "lobby-two");

    public MatchStoreTestContext(OrchestratorOptions? options = null)
    {
        Options = options ?? new OrchestratorOptions();
        Store = new InMemoryMatchStore(Microsoft.Extensions.Options.Options.Create(Options), Clock);
    }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public OrchestratorOptions Options { get; }

    public InMemoryMatchStore Store { get; }

    public static PlayerIdentity Player(string platformUserId) => new(Platform.Steam, platformUserId);

    public static IReadOnlyList<PlayerIdentity> Roster(params string[] platformUserIds) =>
        platformUserIds.Select(Player).ToArray();

    public static string UsernameOf(string platformUserId) => $"{platformUserId}-display-name";

    public JoinOutcome Join(LobbyKey lobby, string platformUserId, IReadOnlyList<PlayerIdentity> roster, string nextSceneName) =>
        Store.TryJoin(lobby, Player(platformUserId), UsernameOf(platformUserId), roster, nextSceneName);

    public AssembledMatch AssembleRoster(LobbyKey lobby, string nextSceneName, params string[] platformUserIds)
    {
        var roster = Roster(platformUserIds);
        var outcomes = platformUserIds.Select(id => Join(lobby, id, roster, nextSceneName)).ToArray();
        var rosterComplete = Assert.Single(outcomes.OfType<RosterComplete>());
        return new AssembledMatch(rosterComplete.Snapshot, outcomes);
    }

    public AssembledMatch StartMatch(LobbyKey lobby, string nextSceneName, params string[] platformUserIds)
    {
        var assembled = AssembleRoster(lobby, "TestScene", platformUserIds);
        Assert.Equal(
            MarkReadyOutcome.MatchStarted,
            Store.MarkReady(assembled.Snapshot.MatchId, assembled.Snapshot.MatchKey));
        return assembled;
    }

    public void AbortRequestOf(JoinOutcome outcome, string platformUserId) =>
        Store.DeregisterAbortedMember(MatchIdOf(outcome), Player(platformUserId), MemberGenerationOf(outcome));

    public static Task<JoinResult> CompletionOf(JoinOutcome outcome) => outcome switch
    {
        MemberAdded added => added.Completion,
        RosterComplete rosterComplete => rosterComplete.Completion,
        _ => throw new InvalidOperationException($"{outcome} carries no waiter.")
    };

    public static async Task<JoinFailed> FailureOf(JoinOutcome outcome) =>
        Assert.IsType<JoinFailed>(await CompletionOf(outcome).WaitAsync(WaiterCompletionBudget));

    public static async Task<JoinSucceeded> SuccessOf(JoinOutcome outcome) =>
        Assert.IsType<JoinSucceeded>(await CompletionOf(outcome).WaitAsync(WaiterCompletionBudget));

    public static Guid MatchIdOf(JoinOutcome outcome) => outcome switch
    {
        MemberAdded added => added.MatchId,
        RosterComplete rosterComplete => rosterComplete.MatchId,
        _ => throw new InvalidOperationException($"{outcome} carries no match id.")
    };

    public static long MemberGenerationOf(JoinOutcome outcome) => outcome switch
    {
        MemberAdded added => added.MemberGeneration,
        RosterComplete rosterComplete => rosterComplete.MemberGeneration,
        _ => throw new InvalidOperationException($"{outcome} carries no member generation.")
    };
}

internal sealed record AssembledMatch(MatchSnapshot Snapshot, IReadOnlyList<JoinOutcome> Outcomes)
{
    public JoinOutcome OutcomeAt(int index) => Outcomes[index];
}
