namespace ResonanceServerOrchestrator.Contracts;

public sealed record LeaveMatchDto
{
    public required PlatformUserInformationDto PlatformUserInformation { get; init; }
}
