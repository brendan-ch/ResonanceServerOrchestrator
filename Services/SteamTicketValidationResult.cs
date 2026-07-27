namespace ResonanceServerOrchestrator.Services;

public sealed record SteamTicketValidationResult(
    bool IsValid,
    string? SteamId,
    bool IsBanned,
    string? FailureDetail)
{
    public static SteamTicketValidationResult Authenticated(string steamId) =>
        new(IsValid: true, steamId, IsBanned: false, FailureDetail: null);

    public static SteamTicketValidationResult IdentityNotAsserted() =>
        new(IsValid: true, SteamId: null, IsBanned: false, FailureDetail: null);

    public static SteamTicketValidationResult Rejected(string failureDetail) =>
        new(IsValid: false, SteamId: null, IsBanned: false, failureDetail);

    public static SteamTicketValidationResult RejectedAsBanned(string steamId, string failureDetail) =>
        new(IsValid: false, steamId, IsBanned: true, failureDetail);
}
