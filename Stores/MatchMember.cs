using Resonance.Contracts;

namespace ResonanceServerOrchestrator.Stores;

internal sealed record MatchMember(
    PlayerIdentity Identity,
    string Username,
    string ServerAuthToken,
    string IpAddress,
    long MemberGeneration,
    TaskCompletionSource<JoinResult> Completion)
{
    public static MatchMember Register(
        PlayerIdentity identity,
        string username,
        string serverAuthToken,
        string ipAddress,
        long memberGeneration) =>
        new(identity,
            username,
            serverAuthToken,
            ipAddress,
            memberGeneration,
            new TaskCompletionSource<JoinResult>(TaskCreationOptions.RunContinuationsAsynchronously));

    public MatchMemberDto ToDto() =>
        new(Identity.Platform, Identity.PlatformUserId, Username, ServerAuthToken, IpAddress);
}
