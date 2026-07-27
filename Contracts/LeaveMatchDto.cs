namespace ResonanceServerOrchestrator.Contracts;

public sealed record LeaveMatchDto
{
    public required IPlatformUserInformationDto PlatformUserInformation { get; init; }
}
